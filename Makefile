# Graft stack. The HTTP host is not part of this demo.
# See readme.md for the full runbook.

compose:
	docker compose -f src/docker-compose.graft.yml up --build -d

down:
	docker compose -f src/docker-compose.graft.yml down
