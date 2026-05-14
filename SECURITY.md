# Security Policy

## Supported Versions

The latest minor release on the `main` branch receives security fixes. Older versions are
not actively patched — please upgrade.

## Reporting a Vulnerability

If you believe you have found a security vulnerability in Moonforge, please **do not** open
a public issue. Instead, report it privately through GitHub's
[Security Advisories](https://github.com/3583Bytes/moonforge-rpg-engine/security/advisories/new)
flow, or email the maintainers.

When reporting, please include:

- A description of the issue and its impact.
- Steps to reproduce (or a minimal proof-of-concept).
- The Moonforge version (or commit SHA) the issue affects.

We aim to acknowledge new reports within 5 business days and to issue a fix or workaround
guidance within 30 days for confirmed vulnerabilities.

## Scope

Moonforge is a deterministic gameplay simulation library. The most relevant security
considerations are:

- **Save-file deserialization.** `JsonGameStateSerializer` parses JSON produced by the
  engine itself — but consuming hostile saves (e.g. cloud-synced from untrusted sources)
  could trigger denial-of-service or unexpected state. Report any deserialization issues.
- **Formula evaluation.** `IFormulaEvaluator` implementations may execute arbitrary
  expressions. The `NoOpFormulaEvaluator` shipped with the engine has no execution risk;
  custom evaluators are the integrator's responsibility.

Issues unrelated to security (gameplay bugs, balance, performance) should be filed as
regular issues.
