"use strict";
// ══════════════════════════════════════
//  BACKEND API
//  Talks to the Express server at /api/scores.
//  Falls back gracefully when the backend is unavailable.
// ══════════════════════════════════════

const API_BASE = "/api";

/**
 * Submit a completed game score to the global backend.
 * Returns { rank, list } on success, or null on failure.
 */
async function submitScore({ name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught }) {
  try {
    const res = await fetch(`${API_BASE}/scores`, {
      method:  "POST",
      headers: { "Content-Type": "application/json" },
      body:    JSON.stringify({ name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught }),
    });
    if (!res.ok) return null;
    return await res.json();
  } catch {
    return null;
  }
}

/**
 * Fetch the global top-10 for a difficulty.
 * Returns an array of score objects, or null on failure.
 */
async function fetchGlobalScores(difficulty) {
  try {
    const res = await fetch(`${API_BASE}/scores/${difficulty}`);
    if (!res.ok) return null;
    const data = await res.json();
    return data.list || null;
  } catch {
    return null;
  }
}
