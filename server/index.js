"use strict";
require("dotenv").config();

const express = require("express");
const path    = require("path");
const db      = require("./db");

const app  = express();
const PORT = process.env.PORT || 3000;

app.use(express.json());

// Serve the game (static files from public/)
app.use(express.static(path.join(__dirname, "..", "public")));

// ── GET /api/scores/:difficulty ───────────────────────────────────────────────
// Returns the global top-10 for a given difficulty ("barn" or "vuxen").
app.get("/api/scores/:difficulty", async (req, res) => {
  const { difficulty } = req.params;
  if (!["barn", "vuxen"].includes(difficulty)) {
    return res.status(400).json({ error: "Invalid difficulty" });
  }
  try {
    const list = await db.getTop(difficulty);
    res.json({ list });
  } catch (err) {
    console.error("GET /api/scores error:", err.message);
    res.status(500).json({ error: "Database error" });
  }
});

// ── POST /api/scores ──────────────────────────────────────────────────────────
// Body: { name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught }
// Returns: { rank, list }
app.post("/api/scores", async (req, res) => {
  const { name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught } = req.body;

  if (!name || typeof score !== "number" || !["barn", "vuxen"].includes(difficulty)) {
    return res.status(400).json({ error: "Invalid payload" });
  }

  const safeName = String(name).slice(0, 20).trim() || "Anonym";
  const safeScore = Math.max(0, Math.floor(score));

  try {
    const result = await db.addScore({
      name: safeName,
      score: safeScore,
      difficulty,
      maxStreak: Math.max(0, Math.floor(maxStreak) || 0),
      maxMulti:  Math.max(1, Math.floor(maxMulti)  || 1),
      perfectRounds: Math.max(0, Math.floor(perfectRounds) || 0),
      caught: Math.max(0, Math.floor(caught) || 0),
    });
    res.json(result);
  } catch (err) {
    console.error("POST /api/scores error:", err.message);
    res.status(500).json({ error: "Database error" });
  }
});

// ── Start ─────────────────────────────────────────────────────────────────────
db.init()
  .then(() => {
    app.listen(PORT, () => {
      console.log(`🦄  Ellies Unicorn Game server running on http://localhost:${PORT}`);
    });
  })
  .catch((err) => {
    console.error("Failed to initialise database:", err.message);
    process.exit(1);
  });
