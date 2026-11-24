# Study chatbot local/Postman guide

Set these environment variables (or their equivalent secure deployment configuration) before using a chat route:

```text
StudyChatbot__BaseUrl=http://localhost:8001/
StudyChatbot__ServiceKey=<same value as CHAT_SERVICE_KEY>
StudyChatbot__TimeoutSeconds=90
Redis__ConnectionString=localhost:6379
Chat__InactivityTtlSeconds=10800
```

Start Redis with `docker run --rm -p 6379:6379 redis:7-alpine`. Start the Python service from `AI/Study Chatbot` with `CHAT_SERVICE_KEY` and `GEMINI_API_KEY` set, then run `uvicorn api:app --port 8001`. Start the backend normally and use a Student JWT in `Authorization: Bearer <token>`.

Outside-chat flow:

1. `POST /api/chat/sessions` creates or resumes the student's only active chat.
2. `POST /api/chat/sessions/{chatId}/messages` with `{ "message": "Explain mitosis" }` works without a file.
3. `POST /api/chat/sessions/{chatId}/attachments` as `multipart/form-data`, field `file`, accepts one PDF, PNG, or JPEG up to 5 MiB. File signatures are checked before OCR. It may be used after text messages and replaces the active attachment while preserving visible history.
4. `DELETE /api/chat/sessions/{chatId}/attachments` removes only the active attachment and returns the chat to text-only mode.
5. `GET /api/chat/sessions/current` restores complete history without moving its expiry.
6. `DELETE /api/chat/sessions/{chatId}`, then create again for the approved New Chat flow.

Inside-study-session flow:

1. `POST /api/study-sessions/{sessionId}/chat/start` creates or resumes a chat using the persisted `AiExtractedText` (no OCR).
2. `POST /api/study-sessions/{sessionId}/chat/messages` with `{ "message": "Explain this concept" }`.
3. `GET /api/study-sessions/{sessionId}/chat` restores it without refreshing its TTL.
4. `POST /api/study-sessions/{sessionId}/chat/end` removes only the temporary chat, never the study session.

Successful create, attachment, and message activity set all Redis keys back to `Chat__InactivityTtlSeconds`; reads and resumes do not. For local expiry testing set it temporarily to a small value. A Python `409` causes one transparent reinitialization from the Redis/DB material and retry; OCR is not repeated.
