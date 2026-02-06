---
name: openclaw-gcn-search
description: "Automate VBDLIS (bgi.mplis.gov.vn) \"Cung cấp thông tin giấy chứng nhận\" lookups using Playwright on headless Ubuntu/OpenClaw. Use when the user asks to tra cứu/tìm kiếm GCN theo Số giấy tờ (CCCD/CMND) hoặc Số phát hành, including multiple inputs per field, and expects text/JSON output (not screenshots)."
---

# OpenClaw GCN Search (Headless Playwright)

## Goal

Run VBDLIS GCN lookups on a no-GUI Ubuntu server with Playwright installed, supporting:
- Search by `Số giấy tờ` (CCCD/CMND)
- Search by `Số phát hành`
- Multiple input values per field (comma or newline separated)

## Quick Start

Use the bundled script:

```bash
node ./scripts/gcn_search.js --soGiayTo 027083000849
node ./scripts/gcn_search.js --soPhatHanh "DL 071888"
node ./scripts/gcn_search.js --soGiayTo "027083000849, 027083000850" --json
node ./scripts/gcn_search.js --soGiayTo "027083000849" --soPhatHanh "DL 071888"
```

Default behavior:
- Headless by default (`--headless true`)
- Uses persistent profile dir `./mplis-profile` (cache + storage)
- Supports batch queries with safety cap `--maxQueries` (default 50)

## Inputs and Parsing Rules

- Accept `--soGiayTo` and/or `--soPhatHanh`.
- Multiple values: comma, semicolon, or newline separated.
- Trim whitespace and deduplicate while keeping order.
- Preserve leading zeros (do not coerce to number).

Batch behavior:
- If only one field is provided: run one query per value.
- If both fields are provided and each has multiple values: run cartesian pairs.
- Use `--maxQueries` to raise the safety limit if needed.

## Credentials and Session

- If the session is expired and the page redirects to login, the script uses:
  - `MPLIS_USER` and `MPLIS_PASS` from environment or `.env`.
- Persistent profile speeds up repeat runs and keeps cookies.

## Output

Text mode (default):
- Summary per query: URL, status line, row count.
- Rows show: Thông tin GCN, Chủ sở hữu, Thửa đất/Căn hộ.

JSON mode (`--json`):
- `{ queries: [...], results: [...] }`
- Each result contains: `query`, `url`, `info`, `count`, `rows` or `error`.

## Troubleshooting

- If page load hangs, check readiness selectors and login flow.
- If the table is empty, verify input formatting and try again.
- If the system blocks too many requests, reduce batch size and add delay.

For page selectors and readiness anchors, read:
- `references/selectors-and-page.md`
