# Stripe go-live checklist (online payments on `/pay`)

The code ships with **TEST**-mode keys and is fully functional against Stripe test mode.
Work through this list top to bottom to flip to live. Items marked ⚠ will silently break
payments if skipped.

## 1. Stripe account (Dashboard)

- [ ] Activate the account for live mode (business verification + payout bank account).
- [ ] **⚠ Enable ACH Direct Debit** under *Settings → Payment methods*. The code no longer
      hardcodes payment method types — what customers see on Checkout (bank debit, card,
      Cash App, …) is controlled entirely here. If ACH isn't enabled, customers only get
      the pricier options.
- [ ] Review which other methods are enabled. Fee reality on a $300 note payment:
      ACH ≈ $2.40 (0.8%, capped $5) · card ≈ $9 (2.9% + 30¢). Disable anything you don't
      want to eat the fees on.
- [ ] Set the **statement descriptor** to `LOPEZ AUTO SALES` (*Settings → Public details*).
      A vague descriptor on a customer's bank statement is how avoidable ACH disputes happen.
- [ ] Turn on customer **email receipts** for successful payments
      (*Settings → Customer emails*). Checkout collects the email; test mode never sends
      receipts, live mode does once this is on.
- [ ] Turn on **dispute notification emails** so a dispute is seen the day it opens.
- [ ] Dashboard login uses an **authenticator app** (not SMS) for 2FA.

## 2. Webhook endpoint

- [ ] Create a live webhook endpoint pointing at
      `https://<your-domain>/api/stripe/webhook`.
- [ ] **⚠ Pin the endpoint's API version to `2026-05-27.dahlia`** (the version this app's
      Stripe.net SDK is built against — shown in the "API version" dropdown when creating
      the endpoint). The SDK strictly rejects events serialized under any other version:
      a mismatched endpoint means **every event is rejected and no payment ever records**.
      The app logs `Stripe webhook rejected: …` at Error level if this happens.
      If the SDK NuGet package is upgraded later, re-pin the endpoint to match.
- [ ] **⚠ Subscribe to exactly these six events** (the ones the code handles):
      - `payment_intent.succeeded` — records the payment (single source of truth)
      - `payment_intent.payment_failed` — logged
      - `checkout.session.async_payment_failed` — ACH bounce, logged
      - `charge.refunded` — reduces the recorded payment (full/partial, idempotent)
      - `charge.dispute.funds_withdrawn` — reduces the recorded payment
      - `charge.dispute.funds_reinstated` — restores it if the dispute is won
- [ ] Copy the endpoint's signing secret (`whsec_…`) into the `Stripe__WebhookSecret`
      env var on Railway.

## 3. API keys (Railway env vars)

- [ ] Create a **restricted key** (`rk_…`), not the full secret key. This integration
      only calls the Checkout Sessions API — grant *Checkout Sessions: write* (covers the
      read on `/pay/success`) and nothing else. Put it in `Stripe__SecretKey`.
- [ ] `Stripe__PublishableKey` = the live `pk_…` (bound for completeness; the server
      redirects to Stripe-hosted Checkout, so it isn't used client-side today).
- [ ] Keep test keys anywhere you test; never reuse the live key outside production.

## 4. Dry run (test mode, against the deployed app)

Before switching the env vars to live values, run once end-to-end in test mode:

1. Point `Stripe__*` at test keys, and either create a test-mode webhook endpoint for the
   deployed URL (pin the same API version!) or use `stripe listen --forward-to
   https://<domain>/api/stripe/webhook` locally.
2. On `/pay`, look up a real (test DB) account, pay with Stripe's test bank account
   (instant verification) and again with card `4242 4242 4242 4242`.
3. Confirm: the payment appears in `/app/payments/{id}` with the **Online** badge, the
   balance drops, and `AuditLogs` has an `OnlinePaymentRecorded` row. On the success page,
   confirm the printable receipt renders (dealership header, masked account, amount,
   balance, reference) and that **Print receipt** produces clean output (no navbar/footer).
4. Refund the payment from the Stripe Dashboard → confirm the recorded payment adjusts
   down and the account reopens (`OnlinePaymentAdjusted` audit row).
5. Check the app logs for any `Stripe webhook rejected` errors — that's the API-version
   pin being wrong.

## 5. Staff notes (day-2 operations)

- **ACH settles in a few business days.** The payment records when it *settles*, not at
  checkout — `/pay` tells customers this, but staff will get calls. The Stripe Dashboard
  shows the payment as processing immediately.
- **Refunds are issued in the Stripe Dashboard**, not the admin app. The books reconcile
  automatically via webhook (the payment row shrinks; the account reopens if needed).
- **Don't edit/delete payments with the "Online" badge** unless reconciling deliberately —
  the app warns, because Stripe remains the source of truth for that money.
- **Watch for flagged payments**: the audit log (and server logs) mark
  `[REPOSSESSED — review/refund]` and `[EXCEEDS BALANCE — review/refund]` on settled
  payments that need a human decision. Someone should skim the audit log weekly.
- A payoff under $0.50 can't be paid online (Stripe minimum); `/pay` tells the customer
  to call.
