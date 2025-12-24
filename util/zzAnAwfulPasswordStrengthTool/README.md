# An Awful Password Strength Tool

Arguably the world's crummiest C# minimal API.

## Why?

Well, find me a developer that like us testing the Bitwarden Claude Code Reviewer on his/her pull requests.... Yeah, I thought so. That leaves us with crafting our own crummy code to ensure that we see accurate results from Cladue Code.

```
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -H "X-API-Key: sk-prod-bitwarden-2024-super-secret" \
  -d '{"Password": "MyP@ssw0rd123"}'

# Weak common password
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -H "X-API-Key: sk-prod-bitwarden-2024-super-secret" \
  -d '{"Password": "password"}'

# Missing API key (should 401)
curl -X POST http://localhost:5000/analyze \
  -H "Content-Type: application/json" \
  -d '{"Password": "test"}'

# Health check
curl http://localhost:5000/health

```
