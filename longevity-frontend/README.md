## Running Locally

```bash
# Run in development mode
dotnet watch run

dotnet run
```

## Connecting to Backend

Update the API base URL in `Program.cs` if your backend is running on a different port or host.

## Docker
```bash
docker build -t longevity-frontend:local .
docker run --rm -d --name longevity-frontend -p 8080:80 longevity-frontend:local
```

open:
- http://localhost:8080
 done
## Docker Compose

```bash
docker compose up -d --no-build
docker compose down


docker compose start
docker compose up -d
docker compose ps
docker compose logs -f
```