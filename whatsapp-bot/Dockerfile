FROM node:20-slim

# Instalar dependências do Puppeteer/Chromium
RUN apt-get update && apt-get install -y \
    chromium \
    chromium-sandbox \
    fonts-liberation \
    libappindicator3-1 \
    libasound2 \
    libatk-bridge2.0-0 \
    libatk1.0-0 \
    libcups2 \
    libdbus-1-3 \
    libgdk-pixbuf2.0-0 \
    libnspr4 \
    libnss3 \
    libx11-xcb1 \
    libxcomposite1 \
    libxdamage1 \
    libxrandr2 \
    xdg-utils \
    --no-install-recommends \
    && rm -rf /var/lib/apt/lists/*

# Definir variável de ambiente para Puppeteer
ENV PUPPETEER_SKIP_CHROMIUM_DOWNLOAD=true
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

WORKDIR /app

# Copiar package.json e instalar dependências
COPY package*.json ./
RUN npm install --production

# Copiar código
COPY . .

# Criar diretórios persistentes
RUN mkdir -p /app/tokens && chmod 777 /app/tokens
RUN mkdir -p /app/sessions && chmod 777 /app/sessions

# Nota: Volume /app/sessions deve ser criado no Railway Dashboard
# Settings → Volumes → New Volume → Mount Path: /app/sessions

EXPOSE 3000

CMD ["node", "server.js"]
