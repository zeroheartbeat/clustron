# Clustron Roadmap

> 🚀 Clustron is a modular, .NET-native clustering framework for building distributed systems — with leader election, heartbeat detection, dynamic membership, and extensibility inspired by JGroups.

---

## ✅ Current Status (Implemented)

- ✔️ **Cluster Membership** (join/leave, peer tracking, role support)
- ✔️ **Leader Election** (coordinator + epoch tracking)
- ✔️ **Heartbeat Monitoring & Failure Detection**
- ✔️ **Custom Cluster Roles** (Member, Client, Observer, MetricsCollector)
- ✔️ **Cluster Messaging** (unicast, broadcast, typed pub/sub events)
- ✔️ **Discovery Provider Abstraction** (`IDiscoveryProvider`)
- ✔️ **Transport Abstraction** (`ITransport`, dynamic override)
- ✔️ **Pluggable Event Bus** (cluster event publishing and listening)
- ✔️ **Metrics Interfaces** (`IMetricsListener`, `IMetricsSnapshotProvider`)
- ✔️ **Testable Client Demo** (`Clustron.Client.Test` with working pub-sub)
- ✔️ **Config-driven Initialization** (`appsettings.json`)

---

## 📦 Version 0.2 – Stabilization & Observability

**Goal: Production-ready internals, basic monitoring, and robust failure handling**

- [ ] Implement metrics recording and snapshot reporting
- [ ] Health monitoring surface (live cluster view via `ClusterMonitor`)
- [ ] Epoch tracking API (`GetCurrentEpoch()` and leader history)
- [ ] Message retry / failure handling for transient issues
- [ ] Code-level and diagnostics logging refinements
- [ ] Add full XML/JSON config support (optional override of appsettings)
- [ ] Benchmark scenarios and performance profiling

---

## 🛠️ Version 0.3 – Protocol Modularity & Discovery Plugins

**Goal: Expand flexibility via stack modularity and dynamic service discovery**

- [ ] Modular protocol pipeline (similar to JGroups stack)
- [ ] Plug-and-play failure detectors (heartbeat, ping-pong, quorum)
- [ ] DNS-based discovery provider
- [ ] File-based or static-list discovery provider
- [ ] Cloud-native discovery plugin (e.g., Azure Blob or AWS S3)
- [ ] Support node tag metadata (for shard-aware logic)
- [ ] Dynamic transport selection via configuration

---

## 💡 Version 0.4 – Built-In Modules & Ecosystem

**Goal: Showcase reusable modules and pave the way for real-world use**

- [ ] Distributed Job Scheduler module
- [ ] Pub/Sub system with topic filtering and QoS (at-least-once, fire-and-forget)
- [ ] Cluster-wide key/value cache module
- [ ] CLI tools or admin shell (status, join/leave, shutdown)
- [ ] Hot-join handling with optional state transfer
- [ ] Health checks & liveness probe APIs for container hosting

---

## 🧠 AI-Oriented Use Cases (Experimental – v0.5+)

- [ ] Federated Learning Coordinator (cluster-wide model sync)
- [ ] Distributed Inference Serving (model router & failover)
- [ ] Swarm agent controller (multi-agent RL coordination)
- [ ] Parameter server coordination (basic training cluster)

---

## 💼 Commercial & Marketplace Plans

- [ ] Azure VM image with BYOL (Clustron preconfigured)
- [ ] AWS AMI/Container (CLI + job scheduler)
- [ ] Support plans (via GitHub Sponsors, Payoneer, or custom)
- [ ] Admin dashboard UI (real-time cluster state)
- [ ] Metrics exporter (Prometheus/OpenTelemetry support)

---

## 📅 Tentative Timeline

| Milestone           | Target Date |
|---------------------|-------------|
| Version 0.2         | July 2025   |
| Version 0.3         | August 2025 |
| Version 0.4         | September 2025 |
| AI Modules          | Q4 2025     |
| Marketplace Release | Q4 2025     |

---

## 🤝 Get Involved

We welcome contributors, feedback, and early adopters.

- Join [Clustron Discord](https://discord.gg/your-link)
- Report issues or request features on [GitHub Issues](https://github.com/your-org/clustron/issues)
- ⭐ Star the project to show support
