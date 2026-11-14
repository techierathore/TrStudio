# AppManager feedback — found while building TrStudio

| | |
|---|---|
| App | TrStudio |
| Upstream | AppManager |
| Updated | 2026-11-14 |

## Summary

1 entry: 0 blocking now, 1 filed and not blocking, 0 fixed upstream.

Nothing is blocked. TrStudio's "Remember me" keeps a sign-in for 30 days by its own limit, because the guide does not say how long a refresh token lives; if AppManager ends it sooner, the user is asked to sign in again with a plain message.

## Entries

### AM-001 — The guide does not say how long a refresh token lives, or whether a refresh extends it

- **Severity:** minor
- **Blocks:** no — TrStudio keeps a ticked sign-in for at most 30 days by its own limit and handles an earlier refusal; the work carried on
- **Repro:** read `docs/AppManager-api-usage-guide.md` §2.3 "Token Refresh" and `POST /AuthSvc/refresh`: the access token lifetime is given (default 1 hour) and each refresh returns a new refresh token, but no section gives the refresh token's own lifetime, whether it is fixed from the first sign-in or renewed by each refresh, or whether it can be set per application
- **Expected:** the refresh token's lifetime (default and maximum), whether it slides on refresh, and whether an application can ask for a longer one (for a desktop "Remember me" of 30 days)
- **Actual:** only the error codes `EXPIRED_REFRESH_TOKEN` and `REVOKED_REFRESH_TOKEN` show that it ends
- **Encountered in:** REQ-FN-099 to REQ-FN-101 (Login, "Remember me", owner decision 6B)
- **Workaround:** the app keeps the token for 30 days from the ticked sign-in, renews it on every open, and treats any refusal as "sign in again"
- **Suggested fix:** one line in §2.3 giving the lifetime, the sliding rule and any per-application setting

## Replies from AppManager
