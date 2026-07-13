<#
.SYNOPSIS
    Deploys SSLH protocol multiplexer and port forwarding to a relay server.

.DESCRIPTION
    Sets up SSLH on a Linux relay server to share a RustDesk port (21118) with
    HTTP traffic for the Computer Inventory API. SSLH detects the protocol and
    routes HTTP to the inventory server while passing RustDesk traffic through.
    
    Also sets up iptables NAT rules as a fallback on the direct port.

.PARAMETER RelayServer
    The IP/hostname of the relay server (where SSLH will be installed)

.PARAMETER DestinationServer
    The IP/hostname of the final destination server (inventory API)

.PARAMETER Port
    The internal API port (default: 5010)

.PARAMETER ExternalPort
    The externally-accessible port to share via SSLH (default: 21118 - RustDesk WebSocket)

.PARAMETER RelaySshUser
    SSH username for relay server (default: mkspvf)

.PARAMETER DestSshUser
    SSH username for destination server (default: hank)

.PARAMETER SkipSSLH
    Skip SSLH setup and only configure iptables port forwarding

.EXAMPLE
    .\deploy-port-forward.ps1 -RelayServer 10.241.24.7 -DestinationServer 10.241.24.60

.EXAMPLE
    .\deploy-port-forward.ps1 -SkipSSLH  # Only set up iptables forwarding

.NOTES
    SSLH allows sharing port 21118 between RustDesk and HTTP traffic.
    Traffic flow: rdp.mkspvf.com:21118 -> SSLH -> HTTP -> 10.241.24.60:5010
                                              -> RustDesk -> localhost:31118
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$RelayServer = "10.241.24.7",
    
    [Parameter(Mandatory=$false)]
    [string]$DestinationServer = "10.241.24.60",
    
    [int]$Port = 5010,
    
    [int]$ExternalPort = 21118,
    
    [string]$RelaySshUser = "mkspvf",
    
    [string]$DestSshUser = "hank",
    
    [switch]$SkipSSLH,
    
    [switch]$Force
)

# RustDesk ports reference
$RustDeskPorts = @(21115, 21116, 21117, 21118, 21119)
$RustDeskLocalPort = 31118  # Local port for RustDesk after SSLH takes over 21118

$RelaySshTarget = "${RelaySshUser}@${RelayServer}"
$DestSshTarget = "${DestSshUser}@${DestinationServer}"

Write-Host "========================================"  -ForegroundColor Cyan
Write-Host " SSLH + Port Forward Deployment"          -ForegroundColor Cyan
Write-Host "========================================"  -ForegroundColor Cyan
Write-Host ""
Write-Host "Relay Server:       $RelayServer (user: $RelaySshUser)" -ForegroundColor Gray
Write-Host "Destination Server: $DestinationServer (user: $DestSshUser)" -ForegroundColor Gray
Write-Host "External Port:      $ExternalPort (shared via SSLH)" -ForegroundColor Gray
Write-Host "API Port:           $Port"                 -ForegroundColor Gray
Write-Host ""
Write-Host "Traffic flow with SSLH:"                   -ForegroundColor Yellow
Write-Host "  rdp.mkspvf.com:${ExternalPort}"          -ForegroundColor Yellow
Write-Host "       |"                                   -ForegroundColor Gray
Write-Host "       v"                                   -ForegroundColor Gray
Write-Host "  SSLH (protocol detection)"                -ForegroundColor Cyan
Write-Host "       |"                                   -ForegroundColor Gray
Write-Host "       +-- HTTP --> ${DestinationServer}:${Port} (Inventory API)" -ForegroundColor Green
Write-Host "       |"                                   -ForegroundColor Gray
Write-Host "       +-- Other -> localhost:${RustDeskLocalPort} (RustDesk)" -ForegroundColor Blue
Write-Host ""

# Confirm before proceeding (unless -Force)
if (-not $Force) {
    $confirm = Read-Host "Proceed with deployment? (y/N)"
    if ($confirm -ne 'y' -and $confirm -ne 'Y') {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
}

#region SSLH Setup
# ============================================================================
# PHASE 1: SSLH DEPLOYMENT
# ============================================================================

if (-not $SkipSSLH) {
    Write-Host ""
    Write-Host "========================================"  -ForegroundColor Magenta
    Write-Host " Phase 1: SSLH Setup"                      -ForegroundColor Magenta
    Write-Host "========================================"  -ForegroundColor Magenta

    $SSLHScript = @'
#!/bin/bash
set -e

EXTERNAL_PORT=__EXTERNAL_PORT__
RUSTDESK_LOCAL_PORT=__RUSTDESK_LOCAL_PORT__
DEST_IP="__DEST_IP__"
API_PORT=__API_PORT__

echo "====================================="
echo " SSLH Protocol Multiplexer Setup"
echo " External Port: $EXTERNAL_PORT"
echo " HTTP -> $DEST_IP:$API_PORT"
echo " RustDesk -> localhost:$RUSTDESK_LOCAL_PORT"
echo "====================================="
echo ""

# Step 1: Check if SSLH is already installed and configured
echo "[1/7] Checking existing SSLH installation..."
if command -v sslh &> /dev/null; then
    SSLH_VERSION=$(sslh -V 2>&1 | head -1 || echo "unknown")
    echo "  SSLH already installed: $SSLH_VERSION"
    SSLH_INSTALLED=true
else
    echo "  SSLH not installed"
    SSLH_INSTALLED=false
fi

# Step 2: Install SSLH if needed
echo "[2/7] Installing SSLH..."
if [ "$SSLH_INSTALLED" = false ]; then
    sudo apt-get update -qq
    sudo apt-get install -y sslh
    echo "  SSLH installed successfully"
else
    echo "  Skipped (already installed)"
fi

# Step 3: Check what's using port 21118 and find RustDesk binaries
echo "[3/7] Checking RustDesk configuration..."
HBBS_PID=$(sudo ss -tlnp | grep ":21118 " | grep -oP 'pid=\K[0-9]+' | head -1 || true)
HBBR_PID=$(sudo ss -tlnp | grep ":21117 " | grep -oP 'pid=\K[0-9]+' | head -1 || true)

# Find where hbbs binary is located
HBBS_PATH=""
if [ -n "$HBBS_PID" ]; then
    HBBS_PATH=$(readlink -f /proc/$HBBS_PID/exe 2>/dev/null || true)
    HBBS_CWD=$(readlink -f /proc/$HBBS_PID/cwd 2>/dev/null || true)
    HBBS_CMDLINE=$(cat /proc/$HBBS_PID/cmdline 2>/dev/null | tr '\0' ' ' || true)
    echo "  hbbs PID: $HBBS_PID"
    echo "  hbbs binary: $HBBS_PATH"
    echo "  hbbs cwd: $HBBS_CWD"
    echo "  hbbs cmdline: $HBBS_CMDLINE"
fi

if [ -n "$HBBR_PID" ]; then
    HBBR_PATH=$(readlink -f /proc/$HBBR_PID/exe 2>/dev/null || true)
    HBBR_CWD=$(readlink -f /proc/$HBBR_PID/cwd 2>/dev/null || true)
    echo "  hbbr PID: $HBBR_PID"
    echo "  hbbr binary: $HBBR_PATH"
    echo "  hbbr cwd: $HBBR_CWD"
fi

# If we couldn't find via proc, try common locations
if [ -z "$HBBS_PATH" ] || [ ! -f "$HBBS_PATH" ]; then
    for path in /root/hbbs /opt/rustdesk/hbbs /usr/local/bin/hbbs /usr/bin/hbbs; do
        if [ -f "$path" ]; then
            HBBS_PATH="$path"
            HBBS_CWD=$(dirname "$path")
            echo "  Found hbbs at: $HBBS_PATH"
            break
        fi
    done
fi

if [ -z "$HBBR_PATH" ] || [ ! -f "$HBBR_PATH" ]; then
    for path in /root/hbbr /opt/rustdesk/hbbr /usr/local/bin/hbbr /usr/bin/hbbr; do
        if [ -f "$path" ]; then
            HBBR_PATH="$path"
            echo "  Found hbbr at: $HBBR_PATH"
            break
        fi
    done
fi

# Step 4: Configure SSLH
echo "[4/7] Configuring SSLH..."

# Create SSLH configuration - use simpler command-line style for older versions
sudo tee /etc/default/sslh > /dev/null << SSLHDEFAULT
# SSLH defaults - managed by deploy-port-forward.ps1
RUN=yes
DAEMON=/usr/sbin/sslh

# Listen on port $EXTERNAL_PORT, route HTTP to inventory API, else to local RustDesk
DAEMON_OPTS="--user sslh --listen 0.0.0.0:$EXTERNAL_PORT --http $DEST_IP:$API_PORT --anyprot 127.0.0.1:$RUSTDESK_LOCAL_PORT --pidfile /var/run/sslh/sslh.pid"
SSLHDEFAULT
echo "  Updated /etc/default/sslh"

# Also create config file (some versions prefer this)
sudo tee /etc/sslh.cfg > /dev/null << SSLHCONFIG
# SSLH Configuration for Computer Inventory + RustDesk
# Generated by deploy-port-forward.ps1

foreground: false;
inetd: false;
numeric: true;
transparent: false;
timeout: 5;
user: "sslh";
pidfile: "/var/run/sslh/sslh.pid";

listen:
(
    { host: "0.0.0.0"; port: "$EXTERNAL_PORT"; }
);

protocols:
(
    { name: "http"; host: "$DEST_IP"; port: "$API_PORT"; },
    { name: "anyprot"; host: "127.0.0.1"; port: "$RUSTDESK_LOCAL_PORT"; }
);
SSLHCONFIG
echo "  Created /etc/sslh.cfg"

# Step 5: Stop existing RustDesk processes and restart on new ports
echo "[5/7] Reconfiguring RustDesk to use alternate ports..."

# Check if RustDesk is running in Docker
HBBS_DOCKER=false
HBBR_DOCKER=false
if sudo docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^hbbs$"; then
    HBBS_DOCKER=true
    echo "  Detected hbbs running in Docker container"
fi
if sudo docker ps --format '{{.Names}}' 2>/dev/null | grep -q "^hbbr$"; then
    HBBR_DOCKER=true
    echo "  Detected hbbr running in Docker container"
fi

# Check if RustDesk is managed by systemd
HBBS_SYSTEMD=false
HBBR_SYSTEMD=false
if [ "$HBBS_DOCKER" = false ]; then
    if systemctl list-unit-files 2>/dev/null | grep -q "hbbs"; then
        HBBS_SYSTEMD=true
    fi
    if systemctl list-unit-files 2>/dev/null | grep -q "hbbr"; then
        HBBR_SYSTEMD=true
    fi
fi

if [ "$HBBS_DOCKER" = true ]; then
    echo "  RustDesk running in Docker containers..."
    echo "  Using docker-compose for reliable deployment..."
    
    # Create docker-compose file for RustDesk with SSLH-compatible ports
    COMPOSE_DIR="/home/mkspvf"
    COMPOSE_FILE="$COMPOSE_DIR/rustdesk-sslh-compose.yml"
    
    echo "  Creating docker-compose file at $COMPOSE_FILE..."
    sudo tee "$COMPOSE_FILE" > /dev/null << COMPOSEFILE
# RustDesk Pro Server - Docker Compose (SSLH Compatible)
# hbbs uses -p to shift base port so SSLH can take 21118
# Generated by deploy-port-forward.ps1

services:
  hbbr:
    image: rustdesk/rustdesk-server-pro:latest
    container_name: hbbr
    command: hbbr
    network_mode: host
    restart: unless-stopped
    volumes:
      - /root:/root
    environment:
      - RUST_LOG=info

  hbbs:
    image: rustdesk/rustdesk-server-pro:latest
    container_name: hbbs
    # -r: relay server address
    # -o: hostname for API (enables API port)
    # -p $RUSTDESK_LOCAL_PORT: shift base port so websocket moves from 21118 to $RUSTDESK_LOCAL_PORT
    # This shifts: ID port to $(($RUSTDESK_LOCAL_PORT - 2)), NAT to $(($RUSTDESK_LOCAL_PORT - 3)), etc.
    command: hbbs -r rdp.mkspvf.com:21117 -o rdp.mkspvf.com -p $RUSTDESK_LOCAL_PORT
    network_mode: host
    restart: unless-stopped
    volumes:
      - /root:/root
    environment:
      - RUST_LOG=info
    depends_on:
      - hbbr

# With -p $RUSTDESK_LOCAL_PORT, hbbs ports become:
#   $(($RUSTDESK_LOCAL_PORT - 4)): API server (Pro only)
#   $(($RUSTDESK_LOCAL_PORT - 3)): NAT test
#   $(($RUSTDESK_LOCAL_PORT - 2)): ID/Rendezvous server
#   $(($RUSTDESK_LOCAL_PORT - 1)): (internal)
#   $RUSTDESK_LOCAL_PORT: Websocket
#
# hbbr ports stay at: 21117 (relay), 21119 (websocket)
#
# SSLH on 21118 routes:
#   HTTP -> $DEST_IP:$API_PORT (Inventory API)
#   Other -> localhost:$RUSTDESK_LOCAL_PORT (RustDesk hbbs websocket)
COMPOSEFILE

    # Stop existing containers
    echo "  Stopping existing RustDesk containers..."
    sudo docker stop hbbs hbbr 2>/dev/null || true
    sudo docker rm hbbs hbbr 2>/dev/null || true
    sleep 2
    
    # Deploy with docker-compose
    echo "  Deploying RustDesk via docker-compose..."
    cd "$COMPOSE_DIR"
    sudo docker compose -f "$COMPOSE_FILE" up -d
    
    sleep 3
    
    # Port mapping with -p $RUSTDESK_LOCAL_PORT (31118):
    # - API: 31116 (Pro only)
    # - NAT test: 31117
    # - ID/Rendezvous (TCP+UDP): 31118 (same as websocket)
    # - Websocket: 31118
    
    # First, clean up ALL existing RustDesk port redirects to avoid duplicates
    echo "  Cleaning up existing iptables redirect rules..."
    # Remove ALL redirects for 21114, 21115, 21116 (loop to catch duplicates)
    for i in 1 2 3 4 5; do
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21114 -j REDIRECT --to-port 31114 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21114 -j REDIRECT --to-port 31116 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21115 -j REDIRECT --to-port 31115 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21115 -j REDIRECT --to-port 31117 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p udp --dport 21115 -j REDIRECT --to-port 31115 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p udp --dport 21115 -j REDIRECT --to-port 31117 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21116 -j REDIRECT --to-port 31116 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p tcp --dport 21116 -j REDIRECT --to-port 31118 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p udp --dport 21116 -j REDIRECT --to-port 31116 2>/dev/null || true
        sudo iptables -t nat -D PREROUTING -p udp --dport 21116 -j REDIRECT --to-port 31118 2>/dev/null || true
        sudo iptables -t nat -D OUTPUT -p tcp --dport 21114 -j REDIRECT --to-port 31114 2>/dev/null || true
        sudo iptables -t nat -D OUTPUT -p tcp --dport 21114 -j REDIRECT --to-port 31116 2>/dev/null || true
    done
    echo "  ✓ Cleaned up old redirect rules"
    
    # Redirect 21114 -> 31116 for RustDesk API (admin panel)
    API_PORT=31116
    echo "  Setting up port redirect 21114 -> $API_PORT for RustDesk API..."
    sudo iptables -t nat -A PREROUTING -p tcp --dport 21114 -j REDIRECT --to-port $API_PORT
    sudo iptables -t nat -A OUTPUT -p tcp -o lo --dport 21114 -j REDIRECT --to-port $API_PORT
    echo "  ✓ Port 21114 redirects to $API_PORT (API/Admin Panel)"
    
    # Redirect 21116 -> 31118 for ID/Rendezvous server (client connections)
    # ID server uses the SAME port as websocket (31118) with -p flag
    echo "  Setting up port redirect 21116 -> $RUSTDESK_LOCAL_PORT for ID server..."
    sudo iptables -t nat -A PREROUTING -p tcp --dport 21116 -j REDIRECT --to-port $RUSTDESK_LOCAL_PORT
    sudo iptables -t nat -A PREROUTING -p udp --dport 21116 -j REDIRECT --to-port $RUSTDESK_LOCAL_PORT
    echo "  ✓ Port 21116 redirects to $RUSTDESK_LOCAL_PORT (ID Server)"
    
    # Redirect 21115 -> 31117 for NAT test
    NAT_TEST_PORT=31117
    echo "  Setting up port redirect 21115 -> $NAT_TEST_PORT for NAT test..."
    sudo iptables -t nat -A PREROUTING -p tcp --dport 21115 -j REDIRECT --to-port $NAT_TEST_PORT
    sudo iptables -t nat -A PREROUTING -p udp --dport 21115 -j REDIRECT --to-port $NAT_TEST_PORT
    echo "  ✓ Port 21115 redirects to $NAT_TEST_PORT (NAT Test)"
    
    # Show final iptables state
    echo "  Final iptables PREROUTING rules:"
    sudo iptables -t nat -L PREROUTING -n | grep -E "211" || true
    
    # Verify new port
    echo "  Checking new port assignments..."
    sudo ss -tlnp | grep -E "hbbs|hbbr|:3111|:2111" | head -10 || true
    
elif [ "$HBBS_SYSTEMD" = true ]; then
    echo "  RustDesk managed by systemd - updating service files..."
    sudo systemctl stop hbbs 2>/dev/null || true
    sudo systemctl stop hbbr 2>/dev/null || true
    
    # Update systemd service to use alternate port
    HBBS_SERVICE_FILE=$(systemctl show hbbs -p FragmentPath 2>/dev/null | cut -d= -f2 || true)
    if [ -n "$HBBS_SERVICE_FILE" ] && [ -f "$HBBS_SERVICE_FILE" ]; then
        sudo sed -i "s|ExecStart=.*hbbs.*|ExecStart=$HBBS_PATH -p $RUSTDESK_LOCAL_PORT|" "$HBBS_SERVICE_FILE" 2>/dev/null || true
        sudo systemctl daemon-reload
    fi
else
    echo "  RustDesk running manually (not systemd)..."
    
    # Kill existing processes
    if [ -n "$HBBS_PID" ]; then
        echo "  Stopping hbbs (PID: $HBBS_PID)..."
        sudo kill $HBBS_PID 2>/dev/null || true
        sleep 1
    fi
    if [ -n "$HBBR_PID" ]; then
        echo "  Stopping hbbr (PID: $HBBR_PID)..."
        sudo kill $HBBR_PID 2>/dev/null || true
        sleep 1
    fi
    
    # Wait for ports to be released
    sleep 2
    
    # Create a startup script for RustDesk with new ports
    if [ -n "$HBBS_PATH" ] && [ -f "$HBBS_PATH" ]; then
        RUSTDESK_DIR=$(dirname "$HBBS_PATH")
        
        echo "  Creating RustDesk startup script at $RUSTDESK_DIR/start-rustdesk.sh..."
        sudo tee "$RUSTDESK_DIR/start-rustdesk.sh" > /dev/null << STARTSCRIPT
#!/bin/bash
# RustDesk startup script - reconfigured for SSLH
# hbbs websocket on port $RUSTDESK_LOCAL_PORT instead of 21118
# Generated by deploy-port-forward.ps1

cd $RUSTDESK_DIR

# Start hbbr (relay) - uses ports 21117, 21119
nohup ./hbbr > hbbr.log 2>&1 &
echo "Started hbbr with PID \$!"

# Wait a moment
sleep 2

# Start hbbs (signaling) with alternate websocket port
# -p sets the websocket port offset from 21118
# To use port $RUSTDESK_LOCAL_PORT: offset = $RUSTDESK_LOCAL_PORT - 21118 = $((RUSTDESK_LOCAL_PORT - 21118))
nohup ./hbbs -p $RUSTDESK_LOCAL_PORT > hbbs.log 2>&1 &
echo "Started hbbs with PID \$! (websocket on port $RUSTDESK_LOCAL_PORT)"

sleep 2
echo ""
echo "RustDesk processes:"
ps aux | grep -E "hbbs|hbbr" | grep -v grep
STARTSCRIPT
        sudo chmod +x "$RUSTDESK_DIR/start-rustdesk.sh"
        
        # Start RustDesk with new ports
        echo "  Starting RustDesk with new port configuration..."
        cd "$RUSTDESK_DIR"
        
        # Start hbbr first (it doesn't use 21118)
        if [ -f "./hbbr" ]; then
            sudo nohup ./hbbr > hbbr.log 2>&1 &
            echo "  Started hbbr (relay server)"
            sleep 2
        fi
        
        # Start hbbs with alternate websocket port
        # The -p flag tells hbbs to use a different websocket port
        if [ -f "./hbbs" ]; then
            sudo nohup ./hbbs -p $RUSTDESK_LOCAL_PORT > hbbs.log 2>&1 &
            echo "  Started hbbs with websocket on port $RUSTDESK_LOCAL_PORT"
            sleep 2
        fi
    else
        echo "  WARNING: Could not find hbbs binary. RustDesk needs manual restart."
    fi
fi

# Step 6: Start SSLH
echo "[6/7] Starting SSLH service..."

# Make sure port 21118 is now free
STILL_USING=$(sudo ss -tlnp | grep ":21118 " | head -1 || true)
if [ -n "$STILL_USING" ]; then
    echo "  WARNING: Port 21118 still in use:"
    echo "  $STILL_USING"
    echo "  Attempting to force-kill..."
    BLOCKING_PID=$(echo "$STILL_USING" | grep -oP 'pid=\K[0-9]+' | head -1 || true)
    if [ -n "$BLOCKING_PID" ]; then
        sudo kill -9 $BLOCKING_PID 2>/dev/null || true
        sleep 2
    fi
fi

# Create PID directory with proper permissions
echo "  Creating SSLH PID directory..."
sudo mkdir -p /var/run/sslh
sudo chown sslh:sslh /var/run/sslh

# Reload systemd and start SSLH
sudo systemctl daemon-reload 2>/dev/null || true
sudo systemctl enable sslh 2>/dev/null || true
sudo systemctl restart sslh

# Wait and verify
sleep 3

# Step 7: Verify everything is running
echo "[7/7] Verifying services..."

echo ""
echo "  Port status:"
sudo ss -tlnp | grep -E ":(21114|21115|21116|21117|21118|21119|31114|31115|31116|31118) " | while read line; do
    echo "    $line"
done

echo ""
if sudo systemctl is-active --quiet sslh; then
    echo "  ✓ SSLH is running on port $EXTERNAL_PORT"
else
    echo "  ✗ SSLH failed to start"
    echo "  Status:"
    sudo systemctl status sslh --no-pager 2>&1 | head -20
fi

# Check if hbbs is running on new port
if sudo ss -tlnp | grep -q ":$RUSTDESK_LOCAL_PORT "; then
    echo "  ✓ RustDesk hbbs websocket on port $RUSTDESK_LOCAL_PORT"
else
    echo "  ✗ RustDesk hbbs not running on expected port"
fi

# Check other RustDesk ports (shifted for SSLH compatibility)
if sudo ss -tlnp | grep -q ":31116 "; then
    echo "  ✓ RustDesk hbbs ID server on port 31116"
elif sudo ss -tlnp | grep -q ":21116 "; then
    echo "  ✓ RustDesk hbbs ID server on port 21116 (not shifted)"
fi
if sudo ss -tlnp | grep -q ":21117 "; then
    echo "  ✓ RustDesk hbbr relay on port 21117"
fi
if sudo ss -tlnp | grep -q ":31114 "; then
    echo "  ✓ RustDesk hbbs API on port 31114"
elif sudo ss -tlnp | grep -q ":21114 "; then
    echo "  ✓ RustDesk hbbs API on port 21114 (not shifted)"
fi

echo ""
echo "====================================="
echo " SSLH Setup Complete!"
echo "====================================="
echo ""
echo "Traffic routing:"
echo "  Port $EXTERNAL_PORT -> SSLH"
echo "    HTTP requests   -> $DEST_IP:$API_PORT (Inventory API)"
echo "    Other (RustDesk) -> localhost:$RUSTDESK_LOCAL_PORT"
echo ""
'@

    # Replace placeholders
    $SSLHScript = $SSLHScript -replace '__EXTERNAL_PORT__', $ExternalPort
    $SSLHScript = $SSLHScript -replace '__RUSTDESK_LOCAL_PORT__', $RustDeskLocalPort
    $SSLHScript = $SSLHScript -replace '__DEST_IP__', $DestinationServer
    $SSLHScript = $SSLHScript -replace '__API_PORT__', $Port

    # Convert to Unix line endings
    $SSLHScript = $SSLHScript -replace "`r`n", "`n"

    Write-Host "[SSLH 1/3] Deploying SSLH configuration to $RelayServer..." -ForegroundColor Green

    # Save script to temp file
    $TempSSLHPath = Join-Path $env:TEMP "sslh-setup.sh"
    [System.IO.File]::WriteAllText($TempSSLHPath, $SSLHScript)

    # Copy script to remote server
    Write-Host "  Copying SSLH script to relay server..." -ForegroundColor Gray
    scp $TempSSLHPath "${RelaySshTarget}:~/sslh-setup.sh"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to copy SSLH script to relay server" -ForegroundColor Red
        exit 1
    }

    # Execute with interactive TTY for sudo password prompt
    Write-Host "  Running SSLH setup (sudo password may be required)..." -ForegroundColor Gray
    ssh -t $RelaySshTarget "chmod +x ~/sslh-setup.sh && ~/sslh-setup.sh && rm ~/sslh-setup.sh"

    # Clean up local temp file
    Remove-Item $TempSSLHPath -Force -ErrorAction SilentlyContinue

    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: SSLH setup may have encountered issues" -ForegroundColor Yellow
        Write-Host "Continuing with iptables setup as fallback..." -ForegroundColor Yellow
    }

    # Verify SSLH is working
    Write-Host ""
    Write-Host "[SSLH 2/3] Verifying SSLH is listening on port $ExternalPort..." -ForegroundColor Green
    
    $sslhRunning = $false
    try {
        # Use ss without sudo - it can show listening ports without root
        $sslhCheck = ssh $RelaySshTarget "ss -tln | grep ':$ExternalPort ' || echo 'PORT_NOT_FOUND'"
        if ($sslhCheck -notmatch "PORT_NOT_FOUND") {
            Write-Host "  SUCCESS: Port $ExternalPort is listening (SSLH)" -ForegroundColor Green
            $sslhRunning = $true
        } else {
            Write-Host "  WARNING: Port $ExternalPort not detected" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "  Could not verify SSLH status" -ForegroundColor Yellow
    }

    # Test HTTP through SSLH
    Write-Host ""
    Write-Host "[SSLH 3/3] Testing HTTP routing through SSLH..." -ForegroundColor Green
    try {
        $response = Invoke-WebRequest -Uri "http://${RelayServer}:${ExternalPort}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: HTTP through SSLH works! (status $($response.StatusCode))" -ForegroundColor Green
        $sslhWorking = $true
    } catch {
        Write-Host "  SSLH HTTP routing not yet working: $($_.Exception.Message)" -ForegroundColor Yellow
        $sslhWorking = $false
    }
}
#endregion

#region Port Forwarding Setup
# ============================================================================
# PHASE 2: IPTABLES PORT FORWARDING (Fallback for direct port access)
# ============================================================================

Write-Host ""
Write-Host "========================================"  -ForegroundColor Magenta
Write-Host " Phase 2: iptables Port Forwarding"       -ForegroundColor Magenta
Write-Host "========================================"  -ForegroundColor Magenta
Write-Host ""
Write-Host "Setting up iptables NAT for direct port $Port access..." -ForegroundColor Gray

# Build the remote script
$RemoteScript = @'
#!/bin/bash
set -e

PORT=__PORT__
DEST_IP="__DEST_IP__"

echo "====================================="
echo " Setting up port forward on $(hostname)"
echo " Port $PORT -> $DEST_IP:$PORT"
echo "====================================="

# Step 1: Enable IP forwarding
echo "[1/5] Enabling IP forwarding..."
sudo sysctl -w net.ipv4.ip_forward=1

# Make persistent
if ! grep -q "^net.ipv4.ip_forward=1" /etc/sysctl.conf 2>/dev/null; then
    echo "net.ipv4.ip_forward=1" | sudo tee -a /etc/sysctl.conf > /dev/null
    echo "  Added to /etc/sysctl.conf"
fi

# Step 2: Configure firewall to allow incoming traffic on port
echo "[2/5] Configuring firewall for port $PORT..."

# Check if ufw is active
if command -v ufw &> /dev/null && sudo ufw status | grep -q "Status: active"; then
    echo "  UFW detected and active"
    # Check if rule already exists
    if ! sudo ufw status | grep -q "$PORT/tcp.*ALLOW"; then
        sudo ufw allow $PORT/tcp comment "Computer Inventory API forward"
        echo "  Added UFW rule: allow $PORT/tcp"
    else
        echo "  UFW rule already exists for port $PORT"
    fi
else
    echo "  UFW not active, checking iptables INPUT chain..."
    # Add iptables INPUT rule if not exists
    if ! sudo iptables -C INPUT -p tcp --dport $PORT -j ACCEPT 2>/dev/null; then
        sudo iptables -I INPUT -p tcp --dport $PORT -j ACCEPT
        echo "  Added iptables INPUT rule for port $PORT"
    else
        echo "  iptables INPUT rule already exists for port $PORT"
    fi
fi

# Step 3: Remove ONLY existing NAT rules for THIS specific port (not RustDesk)
echo "[3/5] Removing existing NAT rules for port $PORT only..."

# Remove PREROUTING rules for this specific port
sudo iptables -t nat -S PREROUTING 2>/dev/null | grep -E "\-\-dport $PORT " | while read -r rule; do
    delete_rule=$(echo "$rule" | sed 's/^-A/-D/')
    echo "  Removing: $delete_rule"
    sudo iptables -t nat $delete_rule 2>/dev/null || true
done

# Remove POSTROUTING rules for this specific destination and port
sudo iptables -t nat -S POSTROUTING 2>/dev/null | grep -E "$DEST_IP.*\-\-dport $PORT" | while read -r rule; do
    delete_rule=$(echo "$rule" | sed 's/^-A/-D/')
    echo "  Removing: $delete_rule"
    sudo iptables -t nat $delete_rule 2>/dev/null || true
done

# Step 4: Add new forwarding rules
echo "[4/6] Adding new iptables NAT rules..."

# DNAT: Redirect incoming traffic
sudo iptables -t nat -A PREROUTING -p tcp --dport $PORT -j DNAT --to-destination $DEST_IP:$PORT
echo "  Added PREROUTING (DNAT) for port $PORT"

# MASQUERADE: Ensure return traffic works
sudo iptables -t nat -A POSTROUTING -p tcp -d $DEST_IP --dport $PORT -j MASQUERADE
echo "  Added POSTROUTING (MASQUERADE) for port $PORT"

# Also need FORWARD rule to allow the forwarded traffic
if ! sudo iptables -C FORWARD -p tcp -d $DEST_IP --dport $PORT -j ACCEPT 2>/dev/null; then
    sudo iptables -I FORWARD -p tcp -d $DEST_IP --dport $PORT -j ACCEPT
    echo "  Added FORWARD rule for port $PORT -> $DEST_IP"
fi

# Also need return traffic FORWARD rule
if ! sudo iptables -C FORWARD -p tcp -s $DEST_IP --sport $PORT -j ACCEPT 2>/dev/null; then
    sudo iptables -I FORWARD -p tcp -s $DEST_IP --sport $PORT -j ACCEPT
    echo "  Added FORWARD return rule for port $PORT <- $DEST_IP"
fi

# Step 5: Check and fix FORWARD chain policy
echo "[5/6] Checking FORWARD chain policy..."
FORWARD_POLICY=$(sudo iptables -L FORWARD -n | head -1 | grep -oP '\(policy \K[A-Z]+')
echo "  Current FORWARD policy: $FORWARD_POLICY"

if [ "$FORWARD_POLICY" = "DROP" ]; then
    echo "  WARNING: FORWARD policy is DROP - this blocks forwarded traffic!"
    echo "  Adding explicit ACCEPT rules for established connections..."
    
    # Allow established/related connections (safer than changing policy)
    if ! sudo iptables -C FORWARD -m state --state ESTABLISHED,RELATED -j ACCEPT 2>/dev/null; then
        sudo iptables -I FORWARD -m state --state ESTABLISHED,RELATED -j ACCEPT
        echo "  Added FORWARD rule for ESTABLISHED,RELATED connections"
    fi
    
    # Alternative: Change policy to ACCEPT (less secure but simpler)
    # Uncomment if the above doesn't work:
    # sudo iptables -P FORWARD ACCEPT
    # echo "  Changed FORWARD policy to ACCEPT"
fi

# Step 6: Save rules
echo "[6/6] Saving iptables rules..."

if command -v netfilter-persistent &> /dev/null; then
    sudo netfilter-persistent save
    echo "  Saved with netfilter-persistent"
elif [ -d /etc/iptables ]; then
    sudo iptables-save | sudo tee /etc/iptables/rules.v4 > /dev/null
    echo "  Saved to /etc/iptables/rules.v4"
elif [ -d /etc/sysconfig ]; then
    sudo iptables-save | sudo tee /etc/sysconfig/iptables > /dev/null
    echo "  Saved to /etc/sysconfig/iptables"
else
    echo "  WARNING: Could not auto-save iptables. Rules may not persist after reboot."
fi

echo ""
echo "====================================="
echo " Setup Complete!"
echo "====================================="
echo ""
echo "Firewall status:"
if command -v ufw &> /dev/null && sudo ufw status | grep -q "Status: active"; then
    sudo ufw status | grep -E "(Status|$PORT)"
else
    echo "  iptables INPUT rules for port $PORT:"
    sudo iptables -L INPUT -n | grep -E "($PORT|dpt:$PORT)" || echo "  (none found - may use default ACCEPT)"
fi
echo ""
echo "Current NAT rules for port $PORT:"
sudo iptables -t nat -L -n -v 2>/dev/null | grep -E "($PORT|$DEST_IP)" | head -10
echo ""
echo "FORWARD rules:"
sudo iptables -L FORWARD -n | grep -E "($PORT|$DEST_IP)" | head -5
echo ""
echo "Verifying RustDesk ports are untouched:"
for rport in 21115 21116 21117 21118 21119; do
    if sudo iptables -t nat -S 2>/dev/null | grep -q "dport $rport.*DNAT"; then
        echo "  Port $rport: Has custom DNAT rule"
    else
        echo "  Port $rport: Clean (no DNAT override)"
    fi
done
echo ""
'@

# Replace placeholders with actual values
$RemoteScript = $RemoteScript -replace '__PORT__', $Port
$RemoteScript = $RemoteScript -replace '__DEST_IP__', $DestinationServer

# Convert to Unix line endings (LF only)
$RemoteScript = $RemoteScript -replace "`r`n", "`n"

Write-Host "[1/4] Deploying port forward rules to $RelayServer..." -ForegroundColor Green

# Save script to temp file
$TempScriptPath = Join-Path $env:TEMP "port-forward-setup.sh"
[System.IO.File]::WriteAllText($TempScriptPath, $RemoteScript)

# Copy script to remote server
Write-Host "  Copying script to relay server..." -ForegroundColor Gray
scp $TempScriptPath "${RelaySshTarget}:~/port-forward-setup.sh"

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to copy script to relay server" -ForegroundColor Red
    exit 1
}

# Execute with interactive TTY for sudo password prompt
Write-Host "  Running script (sudo password may be required)..." -ForegroundColor Gray
ssh -t $RelaySshTarget "chmod +x ~/port-forward-setup.sh && ~/port-forward-setup.sh && rm ~/port-forward-setup.sh"

# Clean up local temp file
Remove-Item $TempScriptPath -Force -ErrorAction SilentlyContinue

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================"  -ForegroundColor Green
    Write-Host " Deployment Successful!"                   -ForegroundColor Green
    Write-Host "========================================"  -ForegroundColor Green
    Write-Host ""
    Write-Host "Testing connectivity chain..." -ForegroundColor Gray
    
    # Test 1: Direct to destination server
    Write-Host ""
    Write-Host "[Test 1/5] Testing direct: http://${DestinationServer}:${Port}/api/health" -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://${DestinationServer}:${Port}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: Destination server responded (status $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: Cannot reach destination directly" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    # Test 2: Through relay server (iptables)
    Write-Host ""
    Write-Host "[Test 2/5] Testing via relay iptables: http://${RelayServer}:${Port}/api/health" -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://${RelayServer}:${Port}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: Relay forwarding works (status $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: Relay not forwarding correctly" -ForegroundColor Red
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    # Test 3: Through SSLH on relay server
    Write-Host ""
    Write-Host "[Test 3/5] Testing via SSLH: http://${RelayServer}:${ExternalPort}/api/health" -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://${RelayServer}:${ExternalPort}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: SSLH routing works (status $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: SSLH routing not working" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    }
    
    # Test 4: Through external domain (direct port)
    Write-Host ""
    Write-Host "[Test 4/5] Testing via domain (direct): http://rdp.mkspvf.com:${Port}/api/health" -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://rdp.mkspvf.com:${Port}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: External domain works (status $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: External domain not reachable" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        Write-Host "  Note: May need DNS propagation or firewall rules" -ForegroundColor Gray
    }
    
    # Test 5: Through external domain via SSLH port
    Write-Host ""
    Write-Host "[Test 5/5] Testing via domain (SSLH): http://rdp.mkspvf.com:${ExternalPort}/api/health" -ForegroundColor Gray
    try {
        $response = Invoke-WebRequest -Uri "http://rdp.mkspvf.com:${ExternalPort}/api/health" -TimeoutSec 10 -UseBasicParsing -ErrorAction Stop
        Write-Host "  SUCCESS: External via SSLH works! (status $($response.StatusCode))" -ForegroundColor Green
    } catch {
        Write-Host "  FAILED: External SSLH route not reachable" -ForegroundColor Yellow
        Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
        Write-Host "  This is expected if port 21118 is already forwarded through your firewall" -ForegroundColor Gray
    }
    
    Write-Host ""
Write-Host "========================================"  -ForegroundColor Cyan
    Write-Host " Connectivity Summary"                     -ForegroundColor Cyan
    Write-Host "========================================"  -ForegroundColor Cyan
    Write-Host ""
    Write-Host "SSLH Route (recommended - uses existing firewall):" -ForegroundColor Green
    Write-Host "  rdp.mkspvf.com:$ExternalPort"           -ForegroundColor White
    Write-Host "       |"                                  -ForegroundColor Gray
    Write-Host "       v"                                  -ForegroundColor Gray
    Write-Host "  SSLH (HTTP detection)"                   -ForegroundColor Cyan
    Write-Host "       |"                                  -ForegroundColor Gray
    Write-Host "       v"                                  -ForegroundColor Gray
    Write-Host "  ${DestinationServer}:$Port (API Server)" -ForegroundColor White
    Write-Host ""
    Write-Host "Direct Route (requires port $Port forwarded):" -ForegroundColor Yellow
    Write-Host "  rdp.mkspvf.com:$Port"                   -ForegroundColor White
    Write-Host "       |"                                  -ForegroundColor Gray
    Write-Host "       v"                                  -ForegroundColor Gray
    Write-Host "  ${RelayServer}:$Port (iptables NAT)"    -ForegroundColor White
    Write-Host "       |"                                  -ForegroundColor Gray
    Write-Host "       v"                                  -ForegroundColor Gray
    Write-Host "  ${DestinationServer}:$Port (API Server)" -ForegroundColor White
    Write-Host ""
    Write-Host "Client Configuration:" -ForegroundColor Cyan
    Write-Host "  Primary URL:  http://rdp.mkspvf.com:$ExternalPort" -ForegroundColor Green
    Write-Host "  Fallback URL: http://10.241.24.60:$Port" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To change destination server in future:" -ForegroundColor Cyan
    Write-Host "  .\deploy-port-forward.ps1 -DestinationServer <NEW_IP>" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "ERROR: Deployment failed!" -ForegroundColor Red
    exit 1
}
