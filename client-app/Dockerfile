FROM node:alpine
WORKDIR /usr/src/app
ENV PATH="./node_modules/.bin:$PATH"
COPY ./client-app/package*.json ./
RUN npm install
COPY ./client-app/ ./
RUN npm run build