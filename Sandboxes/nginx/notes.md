docker build -t nginx -f dockerfile .

docker run -d -p 80:80 -p 443:443 \
    -v C:/repos/AntRunner/Sandboxes/PlantUml:/usr/share/nginx/html \
    nginx