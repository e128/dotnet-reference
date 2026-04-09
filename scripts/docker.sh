#!/usr/bin/env bash
# Docker build, run, and test for E128.Reference.Web.
# Usage: docker.sh [build|run|test|stop|clean] [--no-cache]
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

export DOCKER_BUILDKIT=0

IMAGE="e128-reference-web"
CONTAINER="e128-reference-web-dev"
PORT=8080

CMD="${1:-build}"; shift 2>/dev/null || true
NO_CACHE=""
[[ "${1:-}" == "--no-cache" ]] && NO_CACHE="--no-cache"

wait_healthy() {
    info "Waiting for health..."
    for _ in $(seq 1 30); do
        if curl -sf "http://localhost:$PORT/health" >/dev/null 2>&1; then
            ok "Container healthy at http://localhost:$PORT"
            return 0
        fi
        sleep 1
    done
    err "Container did not become healthy within 30s"
    docker logs "$CONTAINER" 2>&1 | tail -10
    return 1
}

case "$CMD" in
    build)
        info "Building $IMAGE..."
        docker build $NO_CACHE -t "$IMAGE" .
        ok "Image built: $IMAGE"
        ;;

    run)
        docker rm -f "$CONTAINER" 2>/dev/null || true
        info "Starting $CONTAINER on port $PORT..."
        docker run -d --name "$CONTAINER" -p "$PORT:8080" "$IMAGE"
        wait_healthy || exit 1
        ;;

    test)
        info "Building image..."
        docker build -t "$IMAGE" .

        docker rm -f "$CONTAINER" 2>/dev/null || true
        info "Starting container..."
        docker run -d --name "$CONTAINER" -p "$PORT:8080" "$IMAGE"

        if ! wait_healthy; then
            docker rm -f "$CONTAINER" 2>/dev/null
            exit 1
        fi

        PASS=0; FAIL=0

        # Test root endpoint
        BODY=$(curl -sf "http://localhost:$PORT/")
        if [[ "$BODY" == "Hello, World!" ]]; then
            ok "GET / → $BODY"
            PASS=$((PASS + 1))
        else
            err "GET / → expected 'Hello, World!', got '$BODY'"
            FAIL=$((FAIL + 1))
        fi

        # Test health endpoint
        BODY=$(curl -sf "http://localhost:$PORT/health")
        if echo "$BODY" | grep -q '"status":"healthy"'; then
            ok "GET /health → healthy"
            PASS=$((PASS + 1))
        else
            err "GET /health → unexpected: $BODY"
            FAIL=$((FAIL + 1))
        fi

        # Cleanup
        docker rm -f "$CONTAINER" 2>/dev/null

        echo ""
        if [[ $FAIL -eq 0 ]]; then
            ok "All $PASS tests passed"
        else
            err "$FAIL failed, $PASS passed"
            exit 1
        fi
        ;;

    stop)
        docker rm -f "$CONTAINER" 2>/dev/null && ok "Stopped $CONTAINER" || warn "Not running"
        ;;

    clean)
        docker rm -f "$CONTAINER" 2>/dev/null || true
        docker rmi -f "$IMAGE" 2>/dev/null && ok "Removed image $IMAGE" || warn "Image not found"
        ;;

    *)
        err "Usage: docker.sh [build|run|test|stop|clean] [--no-cache]"
        exit 1
        ;;
esac
