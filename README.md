# 🌍 Cosmos DB Multi-Region Demo  
> Demonstrating **Region Affinity**, **Consistency Models**, and **Replication Behavior**

---

## 🚀 Overview

This project showcases how **Azure Cosmos DB** behaves in a **multi-region deployment**, focusing on:

- 🌐 Region Affinity (low-latency writes)
- 🔄 Session Consistency (default behavior)
- ⚡ Eventual Consistency (replication lag under load)

---

## 🏗️ Setup Instructions

### 🔹 Step 1: Create Cosmos DB

- **Account Name:** `ntmscosmos`
- **Regions:**
  - 🇮🇳 Central India
  - 🇦🇺 Australia East
- **Consistency Level (Default):** Session

---

### 🔹 Step 2: Configure Database

| Property        | Value            |
|----------------|------------------|
| Database Name  | `DemoDB`         |
| Container Name | `DemoContainer`  |
| Partition Key  | `/partitionKey`  |

---

### 🔹 Step 3: GitHub Configuration

After creating Cosmos DB:

👉 Copy the **Primary Key**  
👉 Add it to GitHub Secrets:

```
COSMOS_KEY=<your-key>
```

---

## 🧪 Test Scenarios

---

## 🟢 Scenario 1: Region Affinity

### 🎯 Goal:
Prove that each app connects to its **nearest Cosmos DB region**

### 🧪 Steps:

1. Open:
   - 🇮🇳 Central India App URL  
   - 🇦🇺 Australia East App URL  
2. Place them side-by-side

### 👀 Observation:

- Write latency should be:
  - ⚡ ~20ms – 40ms (local region)
- Confirms:
  - ✅ SDK uses `ApplicationRegion`
  - ✅ No cross-region latency

---

## 🔵 Scenario 2: Session Consistency (Default)

### 🎯 Goal:
Observe **near real-time consistency**

### 🧪 Steps:

1. Ensure Cosmos DB consistency = **Session**
2. Perform a write (vote) in 🇦🇺 Australia window

### 👀 Observation:

- 🇦🇺 Local UI updates instantly ⚡
- 🇮🇳 India UI updates within ~1 second

### ✅ Conclusion:

- Strong **read-your-own-write**
- Near real-time global sync

---

## 🟡 Scenario 3: Eventual Consistency & Replication Lag

### 🎯 Goal:
Demonstrate **replication delay under load**

---

### 🧪 Steps:

1. Change Cosmos DB consistency to:
   ```
   Eventual
   ```

2. Open Developer Tools (F12) in 🇦🇺 window

3. Run stress script:

```javascript
for(let i = 0; i < 200; i++) { 
  fetch('/api/vote/Jasprit%20Bumrah', { method: 'POST' }); 
  await new Promise(resolve => setTimeout(resolve, 50));
}
```

---

### 👀 Observation:

- 🇦🇺 Local writes continue smoothly
- 🇮🇳 UI:
  - ⏳ Delayed updates
  - 📈 Sudden bulk jumps

---

### ⚠️ What’s Happening?

- Writes are:
  - Accepted locally
  - Replicated asynchronously 🌐
- Network batching causes:
  - 📦 Burst updates
  - ⏱️ Visible lag

---

## 📊 Key Learnings

| Concept              | Behavior |
|---------------------|---------|
| Region Affinity     | Low latency (~20–40ms) |
| Session Consistency | Near real-time sync |
| Eventual Consistency| Delayed, batched replication |

---

## 💡 Why This Matters

- 🌍 Global apps need **low latency**
- ⚖️ Trade-off between:
  - Consistency
  - Performance
- 🧠 Helps choose correct consistency model

---

## 🛠️ Tech Stack

- Azure Cosmos DB (Multi-region)
- Azure App Service
- JavaScript (Frontend testing)
- REST API

---

## 🎯 Use Cases

- Global voting systems 🗳️  
- Real-time dashboards 📊  
- Distributed applications 🌐  
- High-scale event ingestion ⚡  

---

## ⭐ Final Thoughts

> 🔥 "Consistency is a choice — latency is a consequence."

---

## 📌 Author

**Sanjay NTMS**
