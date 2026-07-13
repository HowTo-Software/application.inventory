# Blazor WebAssembly - serve pre-built static files with nginx
FROM nginx:alpine AS final
COPY publish-web/wwwroot /usr/share/nginx/html
COPY src/ComputerInventory.Web/nginx.conf /etc/nginx/nginx.conf
RUN chmod -R o+rX /usr/share/nginx/html
EXPOSE 8080
