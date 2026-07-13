#!/bin/bash
# Remote setup script for Computer Inventory System

cd ~/computer-inventory

echo "Stopping existing containers..."
docker compose down 2>/dev/null || true

echo "Building and starting containers..."
docker compose up -d --build

echo "Waiting for services to start..."
sleep 15

echo "Container status:"
docker compose ps

echo ""
echo "Setup complete!"
