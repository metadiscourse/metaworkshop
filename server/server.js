const express = require('express');
const app = express();
app.use(express.json());
const cors = require('cors');
app.use(cors());
const sqlite3 = require('sqlite3').verbose();
const db = new sqlite3.Database('./metaworkshop.db');

// Create tables if not exist
db.run(`CREATE TABLE IF NOT EXISTS cards (session_code TEXT, text TEXT, player_id TEXT, phase TEXT, cleaned TEXT)`);
db.run(`CREATE TABLE IF NOT EXISTS bonks (session_code TEXT, cluster_id TEXT, player_id TEXT, timestamp INTEGER)`);
db.run(`CREATE TABLE IF NOT EXISTS combos (session_code TEXT, cluster_id TEXT, combo_count INTEGER, timestamp INTEGER)`);

// Function to clean text for deduplication (case-insensitive, trim whitespace and punctuation from ends)
function cleanText(text) {
  text = text.toLowerCase().trim();
  text = text.replace(/^[.,!?;:"']+|[.,!?;:"']+$/g, '');
  return text;
}

// Submit card (dedup on server)
app.post('/sessions/:code/cards', (req, res) => {
  const code = req.params.code;
  const { text, player_id, phase } = req.body;
  const cleaned = cleanText(text);
  db.get(`SELECT * FROM cards WHERE session_code = ? AND cleaned = ?`, [code, cleaned], (err, row) => {
    if (err) return res.status(500).send();
    if (row) {
      return res.status(409).send({ message: 'Duplicate card' });
    }
    db.run(`INSERT INTO cards (session_code, text, player_id, phase, cleaned) VALUES (?, ?, ?, ?, ?)`, [code, text, player_id, phase, cleaned], (err) => {
      if (err) return res.status(500).send();
      res.status(201).send();
    });
  });
});

// Get all cards for reveal
app.get('/sessions/:code/cards', (req, res) => {
  const code = req.params.code;
  db.all(`SELECT text, cleaned FROM cards WHERE session_code = ?`, [code], (err, rows) => {
    if (err) return res.status(500).send();
    res.send(rows);
  });
});

// Record bonk
app.post('/sessions/:code/bonks', (req, res) => {
  const code = req.params.code;
  const { cluster_id, player_id, timestamp } = req.body;
  db.run(`INSERT INTO bonks (session_code, cluster_id, player_id, timestamp) VALUES (?, ?, ?, ?)`, [code, cluster_id, player_id, timestamp], (err) => {
    if (err) return res.status(500).send();
    res.status(201).send();
  });
});

// Record combo
app.post('/sessions/:code/combos', (req, res) => {
  const code = req.params.code;
  const { cluster_id, combo_count, timestamp } = req.body;
  db.run(`INSERT INTO combos (session_code, cluster_id, combo_count, timestamp) VALUES (?, ?, ?, ?)`, [code, cluster_id, combo_count, timestamp], (err) => {
    if (err) return res.status(500).send();
    res.status(201).send();
  });
});

// Export cards.csv
app.get('/sessions/:code/cards.csv', (req, res) => {
  const code = req.params.code;
  db.all(`SELECT * FROM cards WHERE session_code = ?`, [code], (err, rows) => {
    if (err) return res.status(500).send();
    let csv = 'session_code,text,player_id,phase,cleaned\n';
    rows.forEach(r => {
      csv += `${r.session_code},"${r.text.replace(/"/g, '""')}",${r.player_id},${r.phase},${r.cleaned}\n`;
    });
    res.header('Content-Type', 'text/csv');
    res.attachment('cards.csv');
    res.send(csv);
  });
});

// Export bonks.csv
app.get('/sessions/:code/bonks.csv', (req, res) => {
  const code = req.params.code;
  db.all(`SELECT * FROM bonks WHERE session_code = ?`, [code], (err, rows) => {
    if (err) return res.status(500).send();
    let csv = 'session_code,cluster_id,player_id,timestamp\n';
    rows.forEach(r => {
      csv += `${r.session_code},${r.cluster_id},${r.player_id},${r.timestamp}\n`;
    });
    res.header('Content-Type', 'text/csv');
    res.attachment('bonks.csv');
    res.send(csv);
  });
});

// Export summary.json (includes combos)
app.get('/sessions/:code/summary.json', (req, res) => {
  const code = req.params.code;
  db.all(`SELECT * FROM combos WHERE session_code = ?`, [code], (err, rows) => {
    if (err) return res.status(500).send();
    const summary = {
      num_cards: 0,
      num_bonks: 0,
      combos: rows
    };
    db.get(`SELECT COUNT(*) as count FROM cards WHERE session_code = ?`, [code], (err, row) => {
      summary.num_cards = row ? row.count : 0;
      db.get(`SELECT COUNT(*) as count FROM bonks WHERE session_code = ?`, [code], (err, row) => {
        summary.num_bonks = row ? row.count : 0;
        res.send(summary);
      });
    });
  });
});

app.listen(3000, () => console.log('Server running on port 3000'));