"use strict";
require("dotenv").config();
const { Pool } = require("pg");

const pool = new Pool({ connectionString: process.env.DATABASE_URL });

async function init() {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS scores (
      id           SERIAL PRIMARY KEY,
      name         VARCHAR(20)  NOT NULL,
      score        INTEGER      NOT NULL,
      difficulty   VARCHAR(10)  NOT NULL CHECK (difficulty IN ('barn','vuxen')),
      max_streak   INTEGER      NOT NULL DEFAULT 0,
      max_multi    INTEGER      NOT NULL DEFAULT 1,
      perfect_rounds INTEGER    NOT NULL DEFAULT 0,
      caught       INTEGER      NOT NULL DEFAULT 0,
      created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
    );
    CREATE INDEX IF NOT EXISTS scores_diff_score ON scores (difficulty, score DESC);
  `);
}

/**
 * Insert a score row and return the top-10 list for that difficulty.
 * Returns { rank, list } where list is the top-10 after insertion.
 */
async function addScore({ name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught }) {
  const { rows } = await pool.query(
    `INSERT INTO scores (name, score, difficulty, max_streak, max_multi, perfect_rounds, caught)
     VALUES ($1, $2, $3, $4, $5, $6, $7)
     RETURNING id`,
    [name, score, difficulty, maxStreak, maxMulti, perfectRounds, caught]
  );
  const insertedId = rows[0].id;

  const top = await pool.query(
    `SELECT id, name, score, max_streak AS "maxStreak", max_multi AS "maxMulti",
            perfect_rounds AS "perfectRounds", caught
     FROM scores
     WHERE difficulty = $1
     ORDER BY score DESC, created_at ASC
     LIMIT 10`,
    [difficulty]
  );

  const rank = await pool.query(
    `SELECT COUNT(*) AS cnt FROM scores
     WHERE difficulty = $1 AND score > $2`,
    [difficulty, score]
  );

  return {
    rank: parseInt(rank.rows[0].cnt, 10) + 1,
    list: top.rows,
    insertedId,
  };
}

/**
 * Return top-10 for a difficulty without inserting.
 */
async function getTop(difficulty) {
  const { rows } = await pool.query(
    `SELECT id, name, score, max_streak AS "maxStreak", max_multi AS "maxMulti",
            perfect_rounds AS "perfectRounds", caught
     FROM scores
     WHERE difficulty = $1
     ORDER BY score DESC, created_at ASC
     LIMIT 10`,
    [difficulty]
  );
  return rows;
}

module.exports = { init, addScore, getTop };
