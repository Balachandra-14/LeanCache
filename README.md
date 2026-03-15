# LeanCache

> A distributed in-memory cache built from scratch in C#/.NET — for learning enterprise distributed systems architecture.

[![CI](https://github.com/Balachandra-14/LeanCache/actions/workflows/ci.yml/badge.svg)](https://github.com/Balachandra-14/LeanCache/actions/workflows/ci.yml)

## What is LeanCache?

LeanCache is a Redis-compatible distributed cache built from the ground up. It's a learning project that implements real distributed systems concepts:

- **RESP Protocol** — speaks the same wire protocol as Redis
- **Consistent Hashing** — data partitioned across nodes with virtual nodes
- **Leader-Follower Replication** — sync and async replication modes
- **Gossip Protocol** — SWIM-based node discovery and failure detection
- **Eviction Policies** — LRU, LFU, and configurable memory limits
- **Persistence** — snapshots and append-only file (AOF) durability
- **Automatic Failover** — leader election with quorum-based consensus

## Quick Start

### Using Docker (Recommended)

```bash
# Single node
docker-compose -f docker/docker-compose.yml up

# 3-node cluster with monitoring (Prometheus + Grafana)
docker-compose -f docker/docker-compose.cluster.yml up
```

### Connect with redis-cli

```bash
redis-cli -p 6379
127.0.0.1:6379> PING
PONG
127.0.0.1:6379> SET hello world
OK
127.0.0.1:6379> GET hello
"world"
```

### Build from Source

```bash
# Prerequisites: .NET 8 SDK
dotnet build
dotnet test
dotnet run --project src/LeanCache.Server
```

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  LeanCache  │◄───►│  LeanCache  │◄───►│  LeanCache  │
│   Node 1    │     │   Node 2    │     │   Node 3    │
│  (Leader)   │     │ (Follower)  │     │ (Follower)  │
└──────┬──────┘     └──────┬──────┘     └──────┬──────┘
       │                   │                   │
       └───────────┬───────┘───────────────────┘
                   │
          ┌────────┴────────┐
          │  Consistent     │
          │  Hash Ring      │
          └────────┬────────┘
                   │
          ┌────────┴────────┐
          │  LeanCache      │
          │  Client SDK     │
          └─────────────────┘
```

## Project Structure

```
src/
├── LeanCache.Core/           # Cache engine, data structures, eviction
├── LeanCache.Protocol/       # RESP parser and serializer
├── LeanCache.Server/         # TCP server, command handler, cluster
├── LeanCache.Client/         # .NET client SDK
└── LeanCache.Benchmark/      # Performance benchmarking tool

tests/
├── LeanCache.Core.Tests/
├── LeanCache.Protocol.Tests/
├── LeanCache.Server.Tests/
└── LeanCache.Client.Tests/
```

## Learning Phases

This project is built incrementally. Each phase adds a distributed systems concept:

| Phase | Topic | Status |
|-------|-------|--------|
| 1 | Project Scaffold & CI/CD | ✅ |
| 2 | In-Memory Cache Engine | ⬜ |
| 3 | RESP Protocol Parser | ⬜ |
| 4 | TCP Server & Commands | ⬜ |
| 5 | Docker Deployment | ⬜ |
| 6 | Cluster Discovery (Gossip) | ⬜ |
| 7 | Consistent Hashing | ⬜ |
| 8 | Replication | ⬜ |
| 9 | Client SDK | ⬜ |
| 10 | Eviction Policies | ⬜ |
| 11 | Persistence (Snapshots + AOF) | ⬜ |
| 12 | Automatic Failover | ⬜ |
| 13 | Observability (Prometheus + Grafana) | ⬜ |
| 14 | Benchmarking & Optimization | ⬜ |

## License

[MIT](LICENSE)
