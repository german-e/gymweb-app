#!/usr/bin/env bash
set -e
git pull
docker build -t gymcore:dev .
docker rm -f gymcore 2>/dev/null || true
docker run -d --name gymcore -p 8080:8080 \
  -v gymcore_data:/app/data \
  -v gymcore_keys:/app/keys \
  -v gymcore_uploads:/app/uploads \
  gymcore:dev
echo "Listo: http://localhost:8080"
