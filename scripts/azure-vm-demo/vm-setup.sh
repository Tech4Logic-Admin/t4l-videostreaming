#!/usr/bin/env bash
# ============================================================================
# Tech4Logic Video Search - VM setup (run on the Azure Ubuntu VM after SSH)
# ============================================================================
# Installs Docker + Compose, then optionally clones repo and starts the stack.
# Usage: curl -sSL <url> | bash   OR   bash vm-setup.sh
# ============================================================================

set -e

echo "=== Installing Docker and Docker Compose plugin ==="
sudo apt-get update
sudo apt-get install -y docker.io docker-compose-plugin
sudo usermod -aG docker "$USER"
echo "Docker installed. Run: newgrp docker   (or log out and back in) to use docker without sudo."

echo ""
echo "=== Next steps (run after: newgrp docker) ==="
echo "1) Get the project:"
echo "   Option A (clone):  git clone <your-repo-url> ~/t4l-videostreaming && cd ~/t4l-videostreaming"
echo "   Option B (SCP from your machine):  scp -r /path/to/t4l-videostreaming $USER@<this-vm-ip>:~/"
echo "2) Start the stack with Caddy on port 80:"
echo "   cd ~/t4l-videostreaming"
echo "   docker compose -f docker-compose.yml -f docker-compose.demo.yml up -d --build"
echo "3) Verify: curl http://localhost/api/healthz  and open http://<vm-ip>/ in a browser"
echo ""
