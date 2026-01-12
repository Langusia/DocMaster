<p align="center">
  <img src="assets/icon.png" alt="DocMaster" width="200" />
</p>

<h1 align="center">DocMaster</h1>

<p align="center">
  <strong>Distributed Object Storage with Erasure Coding</strong>
</p>

<p align="center">
  A fault-tolerant, distributed object storage system built with .NET 9.0
</p>

---

## Features

- **Erasure Coding** - RS(x,y) encoding provides fault tolerance with ~ 1.5x storage overhead
- **Smart Replication** - Small files (≤64KB) use simple replication for efficiency
- **Automatic MIME Detection** - Magic byte detection with Office file support
- **Streaming Uploads** - Memory-efficient streaming for large files up to 1GB
- **Self-Healing** - Manual healing support with automatic healing planned

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         DocMaster.Api                           │
│                    (Coordinator Service)                        │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────────────────┐   │
│  │  REST API   │ │  EC Engine  │ │  Node Health Manager    │   │
│  └─────────────┘ └─────────────┘ └─────────────────────────┘   │
└─────────────────────────────┬───────────────────────────────────┘
                              │ gRPC
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐     ┌───────────────┐     ┌───────────────┐
│  Agent Node   │     │  Agent Node   │     │  Agent Node   │
│   (Storage)   │     │   (Storage)   │     │   (Storage)   │
└───────────────┘     └───────────────┘     └───────────────┘
```

## Quick Start

### Prerequisites

- .NET 9.0 SDK
- Docker & Docker Compose
- PostgreSQL 16+

### Running with Docker Compose

```bash
# Clone the repository
git clone https://github.com/Langusia/DocMaster
cd DocMaster

# Start all services (API + 12 storage nodes + PostgreSQL)
docker-compose up -d

# Register storage nodes
curl -X POST http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{"name": "storage-node-01", "grpcAddress": "storage-node-01:5001"}'
# Repeat for all nodes...
```

### Upload a File

```bash
curl -X PUT http://localhost:5000/api/buckets/documents/objects/report.xlsx \
  -H "Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" \
  -H "X-Original-Filename: Q1 Report.xlsx" \
  --data-binary @./report.xlsx
```

### Download a File

```bash
curl -O http://localhost:5000/api/buckets/documents/objects/report.xlsx
```

## Configuration

| Setting | Default | Description |
|---------|---------|-------------|
| `ErasureCoding:DataShards` | 6 | Number of data shards |
| `ErasureCoding:ParityShards` | 3 | Number of parity shards |
| `ErasureCoding:ChunkSizeBytes` | 10MB | Chunk size for large files |
| `ErasureCoding:SmallObjectThreshold` | 64KB | Files ≤ this use replication |
| `ErasureCoding:MaxFileSizeBytes` | 1GB | Maximum file size |

## Storage Strategy

| File Size | Strategy | Copies/Shards | Fault Tolerance |
|-----------|----------|---------------|-----------------|
| ≤ 64KB | Replication | 4 copies | 3 node failures |
| > 64KB | Erasure Coding | 9 shards (6+3) | 3 node failures |

## Project Structure

```
DocMaster/
├── src/
│   ├── DocMaster.Api/           # Coordinator REST API
│   └── DocMaster.Agent.Grpc/    # Storage node agent
├── tests/
│   ├── DocMaster.Api.Tests/
│   └── DocMaster.Agent.Tests/
├── proto/
│   └── storage.proto
├── deploy/
│   └── docker-compose.yml
└── assets/
    └── icon.png
```

## API Reference

### Buckets

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/buckets` | Create bucket |
| GET | `/api/buckets` | List buckets |
| GET | `/api/buckets/{name}` | Get bucket info |
| DELETE | `/api/buckets/{name}` | Delete bucket |

### Objects

| Method | Endpoint | Description |
|--------|----------|-------------|
| PUT | `/api/buckets/{bucket}/objects/{*key}` | Upload object |
| GET | `/api/buckets/{bucket}/objects/{*key}` | Download object |
| HEAD | `/api/buckets/{bucket}/objects/{*key}` | Get object metadata |
| DELETE | `/api/buckets/{bucket}/objects/{*key}` | Delete object |
| GET | `/api/buckets/{bucket}/objects` | List objects |

### Nodes

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/nodes` | Register node |
| GET | `/api/nodes` | List nodes |
| GET | `/api/nodes/{id}` | Get node info |
| DELETE | `/api/nodes/{id}` | Unregister node |

## Deployment Recommendations

For RS(6,3) erasure coding:
- **Minimum**: 9 nodes (no write fault tolerance)
- **Recommended**: 12 nodes (can lose 3 nodes and still write)
- **Production**: 12+ nodes across multiple availability zones

## License

MIT

---

<p align="center">
  Built with ❤️ and .NET 9.0
</p>
