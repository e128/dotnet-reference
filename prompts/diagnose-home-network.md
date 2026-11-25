# Diagnose Wi-Fi and Home Network

Diagnose my Wi-Fi and home network. Find the real bottlenecks behind slow speeds or instability. Guide me through the fixes.

Run these checks in order. Do not run a ping while a throughput test runs. Load pollutes idle latency.

## 1. Wi-Fi Link and RF Congestion

- Inspect the active Wi-Fi link: PHY mode (802.11ac/ax/be), band (2.4 / 5 / 6 GHz), channel, channel width (20 / 40 / 80 / 160 MHz), signal-to-noise ratio, transmit rate and MCS, and interface packet errors.
- Scan nearby networks. Count the competing access points on each channel and band. Name the cleanest channels in my environment.

## 2. Hop-by-Hop Latency and Stability (Idle)

- Ping the default gateway with 30 packets.
- Ping `1.1.1.1` with 30 packets. Do the same over IPv6 when IPv6 is active.
- Run a 5-hop traceroute. Use it to separate Wi-Fi and LAN jitter from ISP latency.
- List active local bandwidth users, VPNs, proxies, and recent Wi-Fi disconnect or roam events.

## 3. DNS Latency

- Show the current DNS servers.
- Compare cached and uncached lookup latency against `1.1.1.1`, `8.8.8.8`, and the router.

## 4. Throughput and Bufferbloat Under Load

- Run `networkQuality -v` on macOS, or the equivalent on Linux and Windows.
- Report downlink, uplink, idle latency, loaded latency, responsiveness (RPM), and ECN and L4S status.

## Report

Give me these four items.

1. A short baseline summary with the exact measurements.
2. The top real bottlenecks, ranked by impact. Skip placebo OS tweaks.
3. Fixes, split into two groups:
   - Safe local quick wins, such as a faster DNS order.
   - Router and modem fixes:
     - Unlock 80 or 160 MHz width on 5 or 6 GHz.
     - Enable Wi-Fi 6, OFDMA, and WMM.
     - Disable band steering or Smart Connect when it degrades stability.
     - Name the cleanest 5 GHz channel and the cleanest 2.4 GHz channel. Use only 1, 6, or 11 on 2.4 GHz. Base the choice on my RF scan.
     - Disable unused guest SSIDs.
     - Check cable and ISP modem signal health: power, SNR, and uncorrectable errors.
     - Recommend SQM when bufferbloat is the cause.
4. A request for my router model, or a paste or screenshot of its Wireless Settings page. Then give exact click-by-click settings.

## Recheck

After I apply the changes, rerun the same benchmark in the same order. Output a Before and After comparison table.
