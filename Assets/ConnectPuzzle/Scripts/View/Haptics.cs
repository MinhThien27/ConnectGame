using UnityEngine;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Rung phản hồi: ngắn, có cường độ, và tắt được.
    ///
    /// KHÔNG dùng Handheld.Vibrate(). Nó là một cú rung ~500ms cố định, không đổi được
    /// độ dài lẫn cường độ. Dùng nó cho "vừa nối thêm một ô" thì kéo ngón qua năm ô là
    /// năm cú nửa giây chồng lên nhau — máy rung liên tục thành một tiếng rè, và thứ đầu
    /// tiên người chơi làm là đi tìm chỗ tắt. Thứ cần ở đây là cú gõ 10-15ms, và trên
    /// Unity chỉ có Vibrator của Android cho được.
    ///
    /// Ba đường vào Android, vì API đổi hai lần:
    ///  - API 31+: lấy Vibrator qua VibratorManager. getSystemService("vibrator") đã bị
    ///             đánh dấu bỏ và trên một số máy trả về null.
    ///  - API 26+: VibrationEffect.createOneShot(ms, amplitude) — có cường độ.
    ///  - dưới đó: vibrate(ms), chỉ có độ dài; cường độ bị bỏ qua.
    ///
    /// Ngoài Android — kể cả trong Editor — mọi hàm ở đây KHÔNG LÀM GÌ, và đó là cố ý:
    /// iOS không có đường tương đương mà không cần plugin native, còn rung trong Editor
    /// thì không có gì để rung. Chỗ gọi vì vậy không cần bọc #if quanh từng lời gọi.
    ///
    /// Cả lớp không bao giờ ném ra ngoài: mọi lời gọi JNI nằm trong try, và hỏng một lần
    /// là tắt hẳn đường rung cho tới lần mở app sau. Rung là thứ phụ — làm ván chơi gãy
    /// vì nó thì tệ hơn hẳn việc không có nó.
    /// </summary>
    public static class Haptics
    {
        /// <summary>
        /// Khoảng cách tối thiểu giữa hai cú rung.
        ///
        /// Cần có vì kéo ngón sinh ra một cú MỖI Ô, mà hai ô sát nhau thì hai cú chỉ cách
        /// vài ms — dồn lại thành tiếng rè dài chứ không còn là từng nhịp gõ. 32ms vẫn giữ
        /// được cảm giác "từng ô một" ở tốc độ kéo nhanh nhất mà tay làm được.
        /// </summary>
        private const float MinGapSeconds = 0.032f;

        /// <summary>Bật/tắt. PuzzleGame gán từ PuzzleProgress.Haptics.</summary>
        public static bool Enabled = true;

        private static float lastPulseAt = -1f;

        // ------------------------------------------------------------------
        // Bảng cường độ. Bốn mức là đủ: thêm mức nữa thì ngón tay không phân biệt được,
        // mà mỗi mức lại là một chỗ phải cân lại khi đổi.
        // ------------------------------------------------------------------

        /// <summary>
        /// Nối thêm một ô vào chuỗi. Nhẹ nhất, và là hàm DUY NHẤT bị giãn cách: nó là
        /// hàm duy nhất nổ thành tràng — mỗi ô ngón tay kéo qua một cú.
        /// </summary>
        public static void Tick() => Fire(9, 70, true);

        /// <summary>Băng nứt, đá vỡ, hoàn tác — chuyện có xảy ra nhưng không phải nước đi.</summary>
        public static void Light() => Fire(14, 120, false);

        /// <summary>Ăn một chuỗi thường.</summary>
        public static void Medium() => Fire(22, 175, false);

        /// <summary>Chuỗi lớn, hoặc thua ván.</summary>
        public static void Strong() => Fire(38, 255, false);

        /// <summary>
        /// Nước không hợp lệ: dài hơn và đục hơn Tick. Cùng cường độ với Tick thì "đã nối
        /// được" và "không nối được" rung giống nhau, tức là rung không nói gì cả.
        /// </summary>
        public static void Reject() => Fire(28, 95, false);

        /// <summary>
        /// Thắng ván: hai nhịp. Một nhịp dài không đọc ra là "xong rồi" — nó chỉ là một
        /// cú rung mạnh, giống hệt lúc thua. Nhịp đôi thì phân biệt được mà không cần nhìn.
        ///
        /// Bỏ qua hàng rào giãn cách vì nó là một sự kiện đơn lẻ, không bao giờ nổ liên tiếp.
        /// </summary>
        public static void Success()
        {
            if (!Enabled) return;
            lastPulseAt = Time.unscaledTime;
            Pattern(new long[] { 0, 26, 60, 42 }, new int[] { 0, 200, 0, 255 });
        }

        /// <summary>
        /// `throttled` chỉ đúng với cú rung nổ thành tràng. Các cú một-lần-một-nước thì
        /// KHÔNG được giãn cách: cú rung lúc ăn chuỗi tới ngay sau cú Tick của ô cuối
        /// cùng vừa nối, nên giãn cách nó là để hàng rào nuốt đúng cú quan trọng nhất.
        ///
        /// Mọi đường vẫn ghi lại mốc thời gian, kể cả đường không bị giãn cách — nhờ vậy
        /// cú Tick kế tiếp bị hàng rào chặn sau một cú rung mạnh, thay vì dính vào nó.
        /// </summary>
        private static void Fire(long milliseconds, int amplitude, bool throttled)
        {
            if (!Enabled) return;

            float now = Time.unscaledTime;
            if (throttled && lastPulseAt >= 0f && now - lastPulseAt < MinGapSeconds) return;
            lastPulseAt = now;

            Pulse(milliseconds, amplitude);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject vibrator;
        private static AndroidJavaClass effectClass;
        private static int apiLevel;
        private static bool ready;
        private static bool unavailable;

        /// <summary>
        /// Lấy Vibrator một lần rồi giữ lại. Tra lại mỗi cú rung là mỗi cú một lượt qua
        /// JNI để hỏi cùng một câu — mà cú rung thì nổ mỗi ô người chơi kéo qua.
        /// </summary>
        private static void Prepare()
        {
            if (ready || unavailable) return;

            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    apiLevel = version.GetStatic<int>("SDK_INT");

                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null) { unavailable = true; return; }

                    if (apiLevel >= 31)
                    {
                        using (AndroidJavaObject manager =
                               activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                        {
                            if (manager != null)
                                vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                        }
                    }
                    else
                    {
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    }
                }

                // Máy không có mô-tơ rung thì gọi vibrate() cũng không lỗi, chỉ im lặng
                // không làm gì. Hỏi trước để khỏi trả tiền JNI cho từng cú rung vô ích.
                if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                {
                    unavailable = true;
                    return;
                }

                if (apiLevel >= 26) effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                ready = true;
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[Haptics] Không lấy được Vibrator, tắt rung: " + error.Message);
                unavailable = true;
            }
        }

        private static void Pulse(long milliseconds, int amplitude)
        {
            Prepare();
            if (!ready) return;

            try
            {
                if (effectClass != null)
                {
                    // Cường độ hợp lệ là 1..255. Số 0 KHÔNG phải "im" mà là tham số sai,
                    // và createOneShot ném IllegalArgumentException khi nhận nó.
                    int level = amplitude < 1 ? 1 : (amplitude > 255 ? 255 : amplitude);
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", milliseconds, level))
                        vibrator.Call("vibrate", effect);
                }
                else
                {
                    vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[Haptics] vibrate() lỗi, tắt rung: " + error.Message);
                unavailable = true;
                ready = false;
            }
        }

        /// <summary>
        /// Chuỗi nhịp: timings[i] là độ dài đoạn thứ i, amplitudes[i] là cường độ của nó
        /// (0 = quãng nghỉ). Dưới API 26 không có cường độ, nên rơi về một cú duy nhất —
        /// tổng độ dài các đoạn CÓ rung, để nó vẫn dài hơn một cú thường.
        /// </summary>
        private static void Pattern(long[] timings, int[] amplitudes)
        {
            Prepare();
            if (!ready) return;

            try
            {
                if (effectClass != null)
                {
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createWaveform", timings, amplitudes, -1))
                        vibrator.Call("vibrate", effect);
                    return;
                }

                long total = 0;
                for (int i = 0; i < timings.Length; i++)
                    if (i < amplitudes.Length && amplitudes[i] > 0) total += timings[i];
                vibrator.Call("vibrate", total);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning("[Haptics] createWaveform lỗi, tắt rung: " + error.Message);
                unavailable = true;
                ready = false;
            }
        }
#else
        // Nền tảng không có đường rung. Giữ đúng chữ ký để chỗ gọi không phải bọc #if.
        private static void Pulse(long milliseconds, int amplitude) { }
        private static void Pattern(long[] timings, int[] amplitudes) { }
#endif
    }
}
