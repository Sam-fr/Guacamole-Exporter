🚧🚧🚧
An Apache Guacamole metrics exporter. It directly uses the Guacamole HTTP API. It supports TOTP.

**Docker hub** : https://hub.docker.com/r/sam12100/guacamoleexporter

# Metrics
| **metric** | **value** | **label** | **description**  |
| ----------- | ----------- | ----------- | ----------- |
| guacamole_up | 0 (service not up), 1 (service up) | - | Uses the "/api/patches" endpoint of the Guacamole API to test if the service is responding correctly. |
| guacamole_count_of_user | - | - | Number of Guacamole users (LDAP users do not appear until they have connected at least once). |
| guacamole_count_of_active_connection | - | - | Number of active connections (RDP, SSH, VNC, etc.).|
| guacamole_active_connection | 1 (will always be set to 1) | user, startDateUtc, startDate_str, startDate_order, connected_since, connectionIdentifier, connectionName, connectionProtocol | Active connection. Including the username, connection start date, number of minutes since the connection began, connection ID, connection name, and connection protocol (RDP, SSH, VNC, etc.). | 

<br /> If you want to use TOTP, you need to clear the TOTP on the user. The exporter will automatically handle the first authentication by saving the TOTP key in `/data/totp.txt` <br />
The user must have permissions to query Guacamole on the API endpoints. <br /> <br /> 

# Environment variables
| **variable** | **example** | **default** | **required** | **description** |
| ----------- | ----------- | ----------- | ----------- | ----------- |
| GUACAMOLE_HOSTNAME | https://example.com:8080/guacamole | - | yes | Guacamole Url |
| GUACAMOLE_USERNAME | service-user | - | yes | Guacamole user that the exporter will use to request API |
| GUACAMOLE_PASSWORD | xxx | - | yes | Guacamole user password |
| GUACAMOLE_DATASOURCE | mysql | mysql | no | Datesource of guacamole user |
| LISTEN_PORT | 9134 | 9134 | no | port listen by exporter |
| INTERVAL_CHECK | 30 | 30 | no | Interval to request API in second |
| TIME_SHIFT | 2 | 0 | no | Shifts the connection start time. Negative number possible. |

# Compose
```
services:
  guac_exporter:
    image: sam12100/guacamoleexporter:latest
    ports:
      - 9623:9134
    environment:
      GUACAMOLE_HOSTNAME: https://example.com:8080/guacamole
      GUACAMOLE_USERNAME: prometheus-exporter
      GUACAMOLE_PASSWORD: x
      GUACAMOLE_DATASOURCE: mysql
      TIME_SHIFT: 2
      TZ: Europe/Paris
    restart: unless-stopped
```

If you want to retrieve the TOTP key you can mount volume `/data`
```
    volumes:
      - /srv/guac_exporter/data:/data
```
**Make sure the host folder has the correct permissions**
