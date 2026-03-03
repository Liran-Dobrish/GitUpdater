```mermaid 
flowchart TD
    %% === API Enqueue ===
    A([Client POST /GitUpdate]) --> B[Create QueueValue]
    B --> C[Append to repo queue in Redis]
    C --> D([Return RequestId])

    %% === Background Poll Loop ===
    E([QueueProcessorService poll loop]) --> F[SCAN Redis for repo queues]
    F --> G{Queues found?}
    G -->|No| E
    G -->|Yes| H[Start task per repo]
    H --> I{Claim queue\natomically}
    I -->|Already claimed| J([Skip - another instance owns it])
    I -->|Claimed| K[Clone or pull repo]
    K --> L[Apply file updates]
    L --> M{Files modified?}
    M -->|Yes| N[Git add + commit + push]
    M -->|No| O{More items\nin queue?}
    N --> O
    O -->|Yes| K
    O -->|No| P[Remove queue from Redis]
    P --> Q[Delete local clone]
    Q --> E

    %% === Crash Recovery ===
    R([Pod crash]) -.-> S[Redis key auto-expires\nafter StaleClaimTimeout]

    %% Styling
    style A fill:#e6f3ff,stroke:#4a90d9
    style D fill:#e6f3ff,stroke:#4a90d9
    style E fill:#fff5e6,stroke:#d9a04a
    style J fill:#ffe6e6,stroke:#d94a4a
    style Q fill:#e6ffe6,stroke:#4ad94a
    style R fill:#ffcccc,stroke:#d94a4a