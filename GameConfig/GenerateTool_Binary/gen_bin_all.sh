#!/bin/bash

cd "$(dirname "$0")"
echo "Current directory: $(pwd)"

bash ./gen_bin_client.sh
client_status=$?
bash ./gen_bin_server.sh
server_status=$?
[ "$client_status" -ne 0 ] && exit "$client_status"
exit "$server_status"
