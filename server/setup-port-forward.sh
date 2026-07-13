#!/bin/bash
#
# Port Forward Setup Script
# Run on 10.241.24.7 to forward traffic to 10.241.24.60:5010
#
# Usage: sudo ./setup-port-forward.sh
#

set -e

# Configuration
SOURCE_PORT=5010
DEST_IP="10.241.24.60"
DEST_PORT=5010

echo "========================================"
echo " Port Forward Setup"
echo " Forward: *:${SOURCE_PORT} -> ${DEST_IP}:${DEST_PORT}"
echo "========================================"
echo ""

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "ERROR: Please run as root (sudo)"
    exit 1
fi

# Step 1: Enable IP forwarding
echo "[1/4] Enabling IP forwarding..."
sysctl -w net.ipv4.ip_forward=1

# Make it persistent across reboots
if ! grep -q "^net.ipv4.ip_forward=1" /etc/sysctl.conf; then
    echo "net.ipv4.ip_forward=1" >> /etc/sysctl.conf
    echo "  Added to /etc/sysctl.conf for persistence"
fi

# Step 2: Remove existing rules for this port (clean slate)
echo "[2/4] Removing existing iptables rules for port ${SOURCE_PORT}..."

# Remove PREROUTING rules for this port
iptables -t nat -S PREROUTING 2>/dev/null | grep -- "--dport ${SOURCE_PORT}" | while read -r rule; do
    # Convert -A to -D for deletion
    delete_rule=$(echo "$rule" | sed 's/^-A/-D/')
    echo "  Removing: $delete_rule"
    iptables -t nat $delete_rule 2>/dev/null || true
done

# Remove POSTROUTING rules for destination
iptables -t nat -S POSTROUTING 2>/dev/null | grep -- "${DEST_IP}" | grep -- "--dport ${DEST_PORT}" | while read -r rule; do
    delete_rule=$(echo "$rule" | sed 's/^-A/-D/')
    echo "  Removing: $delete_rule"
    iptables -t nat $delete_rule 2>/dev/null || true
done

echo "  Existing rules cleared"

# Step 3: Add new forwarding rules
echo "[3/4] Adding new iptables rules..."

# DNAT: Redirect incoming traffic on SOURCE_PORT to DEST_IP:DEST_PORT
iptables -t nat -A PREROUTING -p tcp --dport ${SOURCE_PORT} -j DNAT --to-destination ${DEST_IP}:${DEST_PORT}
echo "  Added PREROUTING rule (DNAT)"

# MASQUERADE: Ensure return traffic comes back through this server
iptables -t nat -A POSTROUTING -p tcp -d ${DEST_IP} --dport ${DEST_PORT} -j MASQUERADE
echo "  Added POSTROUTING rule (MASQUERADE)"

# Step 4: Save rules for persistence (works on most distros)
echo "[4/4] Saving iptables rules..."

if command -v netfilter-persistent &> /dev/null; then
    netfilter-persistent save
    echo "  Saved with netfilter-persistent"
elif command -v iptables-save &> /dev/null; then
    # Try common locations
    if [ -d /etc/iptables ]; then
        iptables-save > /etc/iptables/rules.v4
        echo "  Saved to /etc/iptables/rules.v4"
    elif [ -d /etc/sysconfig ]; then
        iptables-save > /etc/sysconfig/iptables
        echo "  Saved to /etc/sysconfig/iptables"
    else
        iptables-save > /root/iptables.rules
        echo "  Saved to /root/iptables.rules (add to startup manually)"
    fi
else
    echo "  WARNING: Could not save rules automatically"
    echo "  Add these commands to /etc/rc.local or equivalent:"
    echo "    iptables -t nat -A PREROUTING -p tcp --dport ${SOURCE_PORT} -j DNAT --to-destination ${DEST_IP}:${DEST_PORT}"
    echo "    iptables -t nat -A POSTROUTING -p tcp -d ${DEST_IP} --dport ${DEST_PORT} -j MASQUERADE"
fi

echo ""
echo "========================================"
echo " Setup Complete!"
echo "========================================"
echo ""
echo "Current NAT rules:"
iptables -t nat -L -n -v | grep -E "(PREROUTING|POSTROUTING|${SOURCE_PORT}|${DEST_IP})" | head -20
echo ""
echo "Test with: curl http://localhost:${SOURCE_PORT}/api/health"
echo ""
