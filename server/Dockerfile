# ── Ellies Unicorn Game — server image ───────────────────────────────────────
FROM node:20-alpine

WORKDIR /app

# Install deps first for better layer caching
COPY server/package*.json ./
RUN npm install --omit=dev

# Copy server code + static game assets
COPY server/ ./
COPY public/ ../public/

EXPOSE 3000

CMD ["node", "index.js"]
