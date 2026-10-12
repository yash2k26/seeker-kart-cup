package com.solana.unity.mwa;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;

/**
 * KartCup patch (13 Oct 2026): one JSON-RPC POST on Android's own HTTP stack, for the blockhash taken while the wallet is
 * in front. Mono's HttpClient failed there on the phone ("An error occurred while sending the request"), so a payment
 * went out with a blockhash built before the wallet opened and expired while the player approved it.
 */
public final class MwaHttp {
    private MwaHttp() {}

    public static String post(String url, String body, int timeoutMs) throws Exception {
        HttpURLConnection c = (HttpURLConnection) new URL(url).openConnection();
        try {
            c.setConnectTimeout(timeoutMs);
            c.setReadTimeout(timeoutMs);
            c.setRequestMethod("POST");
            c.setDoOutput(true);
            c.setRequestProperty("Content-Type", "application/json");
            byte[] out = body.getBytes(StandardCharsets.UTF_8);
            try (OutputStream os = c.getOutputStream()) { os.write(out); }
            int code = c.getResponseCode();
            InputStream in = code >= 400 ? c.getErrorStream() : c.getInputStream();
            ByteArrayOutputStream buf = new ByteArrayOutputStream();
            if (in != null) { byte[] b = new byte[8192]; int n; while ((n = in.read(b)) > 0) buf.write(b, 0, n); in.close(); }
            return buf.toString("UTF-8");
        } finally {
            c.disconnect();
        }
    }
}
