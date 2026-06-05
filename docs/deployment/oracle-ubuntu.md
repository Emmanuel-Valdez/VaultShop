# Oracle Ubuntu Deployment Notes

This document records non-sensitive deployment context for running Ukiyo on an Oracle Cloud Ubuntu server. Do not add secrets, private keys, real connection strings, passwords, API keys, or personal allowlisted IP addresses to this file.

## Current Server

- Provider: Oracle Cloud Infrastructure.
- Shape: ARM Ampere A1 Free Tier.
- Operating system: Ubuntu Server.
- Access: SSH with key authentication from Windows.
- Temporary public URL: use the server public IP until a real domain is configured.

## Oracle Networking

- The VM has a public IP address assigned.
- The route table sends outbound internet traffic through an Internet Gateway.
- The active Oracle firewall layer is the subnet Security List.
- Network Security Groups were removed from the VM to avoid overlapping ingress rules during initial setup.

Required ingress rules:

| Source | Protocol | Port | Purpose |
| --- | --- | --- | --- |
| Admin IP `/32` | TCP | 22 | SSH |
| `0.0.0.0/0` | TCP | 80 | HTTP |
| `0.0.0.0/0` | TCP | 443 | HTTPS |

## Ubuntu Firewall

UFW is enabled and allows:

```text
22/tcp
80/tcp
443/tcp
```

## SSH Hardening

The SSH daemon is configured for key-based access:

```text
PermitRootLogin no
PasswordAuthentication no
PubkeyAuthentication yes
```

Validation commands:

```bash
sudo sshd -t
sudo systemctl restart ssh
```

## Fail2Ban

Fail2Ban is installed and active. The `sshd` jail is enabled.

Useful check:

```bash
sudo fail2ban-client status
```

## Nginx

Nginx is installed, enabled, and responding externally on port 80.

Useful commands:

```bash
sudo systemctl status nginx
sudo ss -tulpn | grep nginx
```

Expected initial test response:

```text
Welcome to nginx!
```

## Resolved Issues

External HTTP initially timed out even though Nginx, localhost, private IP access, Linux routing, UFW, Internet Gateway, and route table were valid.

Resolution:

- Simplified Oracle networking.
- Removed the NSG association from the VM.
- Kept only the subnet Security List for ingress rules.

## Future Deployment Plan

Target architecture:

```text
Internet
-> Oracle Security List / UFW
-> Nginx reverse proxy
-> ASP.NET Core app running under systemd or Docker Compose
-> SQL Server
```

Recommended next steps:

1. Learn and test Docker and Docker Compose basics.
2. Decide whether SQL Server will run in Docker, directly on the VM, or externally.
3. Publish Ukiyo with `dotnet publish` or containerize it.
4. Run the ASP.NET Core app behind Nginx.
5. Configure HTTPS after a domain or temporary DNS hostname is available.
6. Set `SiteUrl` to the final public URL.

## Required Environment Variables

Configure these on the server or in the deployment environment. Do not commit real values.

```text
ConnectionStrings__DefaultConnection=
Stripe__SecretKey=
Stripe__PublishableKey=
Facebook__AppId=
Facebook__AppSecret=
Email__Provider=Fake
Email__UseFakeEmailSender=true
Resend__ApiKey=
Resend__FromEmail=
Seed__AdminEmail=
Seed__AdminPassword=
SiteUrl=
Social__TikTok=
Social__WhatsApp=
Social__Instagram=
Social__Facebook=
Social__Evalmon=
```

## SiteUrl Guidance

`SiteUrl` is used for canonical URLs, Open Graph metadata, `robots.txt`, and `sitemap.xml`.

Until a domain is configured, use the temporary public server URL. After a domain is configured, update the server environment variable without changing application code.

Do not point `SiteUrl` to a domain that belongs to another site.

## Local-Only Notes

If exact IPs, personal SSH notes, or one-off commands need to be preserved, keep them in a local untracked file such as:

```text
docs/deployment/oracle-ubuntu.local.md
```

That file should not be committed.
