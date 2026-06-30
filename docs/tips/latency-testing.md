# Latency testing

**Test** and **Test All** measure **TCP round-trip time** to the server address and port — the same node RTT most clients show (v2rayNG, Hiddify, Nekoray, etc.).

If the node blocks raw TCP connects (some CDN/WAF setups), the app falls back to a short proxy-path probe through a temporary local SOCKS port.

## Results

| Display | Meaning |
|---------|---------|
| `123 ms` | Successful measurement |
| `timeout` | No response within 10 seconds |
| `—` | Test in progress or not run yet |

After **Test All**, results are saved with your server list.
