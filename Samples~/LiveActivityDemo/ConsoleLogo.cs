namespace TappGo.Samples
{
    /// <summary>
    /// The Tapp wordmark, carried in source rather than as an asset.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A UPM sample lives in a <c>Samples~</c> folder, which Unity's asset pipeline never sees — so an image
    /// there would ship without the <c>.meta</c> file that carries its GUID, and every reference to it in an
    /// importing project would resolve to nothing. Embedding the bytes sidesteps the problem entirely: this is
    /// a <c>.cs</c> file, and <c>.cs</c> files import fine.
    /// </para>
    /// <para>
    /// Only the <b>alpha channel</b> is stored, as an 8-bit greyscale PNG, and the colour is applied when it is
    /// drawn. The wordmark is a single flat purple, so its RGB carried no information — dropping it took the
    /// payload from tens of kilobytes to about four, and means the mark now follows
    /// <see cref="ConsoleTheme.Accent"/> instead of fighting it.
    /// </para>
    /// <para>
    /// Regenerate by re-running the mask step in <c>Tools/</c> against
    /// <c>TestApp/Assets.xcassets/logo.imageset/logo.png</c>; do not hand-edit the string.
    /// </para>
    /// </remarks>
    internal static class ConsoleLogo
    {
        /// <summary>The mark's aspect ratio, so a caller need only choose a width.</summary>
        internal const float AspectRatio = 360f / 157f;

        /// <summary>
        /// The alpha mask, base64 of an 8-bit greyscale PNG. Generated — do not edit.
        /// </summary>
        /// <remarks>
        /// Data only: <see cref="ConsoleTheme"/> decodes it, because it is the type that owns every texture the
        /// console makes and destroys them together.
        /// </remarks>
        internal static readonly string[] Mask =
        {
            "iVBORw0KGgoAAAANSUhEUgAAAWgAAACdCAAAAAC8pr0XAAAReklEQVR42u2deVxVxRfARxAFBDdAwBU0XHDJBQVNS9QSFMUN94ys",
            "rNQ2c8dfuVRo7lb+NBfcUfllSqJImUsuqRlmarkkKi64K6CAQPfHqrw365k7+Hn4uefPN/ecmft9c+fOnHNmLkI5Um9JwgPNAiRt",
            "Rx8r9Fgax94vUnYnsh4q6RJwW7MY+aN2Yatc75gVpXcr4ZxHa5Ykp8oVNGsxVnSxVInm3FezLPm+oF1X8aKaJZlznbsWBlobnteu",
            "yoSStiUZ9DeWxlm79myC/sviQGtNn0nQKZYHutczCdryOGuDDNAGaAO0ARouA59J0HeNHv105Ljlge73TIKeaHmg/Z5J0GUvWBrn",
            "O9bPJGjU/pGFgf46v13X8BKPku0mHZJpWR3aJb9Zq7GSK6VLuEO6SVS65XC+3LigVfbRpgX/xj1X4kMsyNa/b6ge2aaMc2LDJ61q",
            "WrRRfeogQ0Yqonx72xtlDJp0mQRgmRm/Nnx491Z1XMtbGeCAMkt4WFg0yNvosdLyuQjj9D1fDqhfyoClQyYLYP4nzNUApVNG8TH/",
            "2sHApFv6czEnDCttYNItHTI4mM+GWhuU9MuLD9mY4wcYczgVYpfAns11NRCpkQ1MzhHlDUJqZAyT8wQDkCIJzWJgjmltAFIkDVgJ",
            "5SEGH2WyigH6NQOPMvFiBMDGG3jUyc90zm8ZdNTJDCrmky8AzJR7zrdjl97BgR19PWypF5VvGdy9o4d0U0u7N+0QFNKrS/umnson",
            "nJWbtA8I7hno71O3mOaywVTOJ+xFbTQLi75kskMl8j3S1omQ+PziX5rINLT26J1FQ883No9oqmqxWn385qQipi9sCWuhnHNZ6pLw",
            "kY+YhYafnSZpb32joul19nFPSr8gm7LyGbVi+5LRvjjA0oP3kOJlS5sJ32izTyJilw73JpS8tCoNN31+hmLWE6gdeoCQfotdVAOp",
            "n9gVvXKJyVqTZKvbmYLSXVXNSgLiKXVkbRSb5TeILXT0+pkWWIfSTGuHXlTIuRJ19+dkEXX3r5gpO7emVXl8aU/ToiDc2LAiiiZv",
            "h04HWJXs8ua3s18GpeY6jJmAlr2mrjLQk/TM6xpt4rlWtUeF68oWZo/nCcznOqKosYywx7/7xPA26Ua05LxDTXZRpfR4PFJ12cJJ",
            "OMr+qbea94DVZUoNnwkoD0sTiuPOy5uEHDT/eYiZtbfNygtwOK4SqWOTM6uh75hd3Sn/Z5cDQqaVxKAH0CZ2/P/RebNoxPyvnNeK",
            "E/brOrNXpXl+3v/yfm6ZIFZHUnNGU/ebXXwk71eneDHTxxorAH2SlT/OlHaXxRNA0t8kTCIvmfm1zMtTcv/rzsIJbjd8qU11wy7O",
            "Hc5aXRI1nfaubs7DKKZXcjv0p9mgvKX5H+DzBdNHEs9Ey3nAhwI29GUtp4Xn38eunY1spmUAmr/KVh9nm+tku7/zgoPWK6EZYqn4",
            "T1VMTOLbxcdAdwJfp0yql2JX7kTTYaZ/r6IL9GBK32jOUwRzJomLiUl8mjjzM6jFu+TFPf4yOeEPTSQ/6aIH9CHKc87Tm62pB42X",
            "n4abjCM2F58e3r4DNv1nZeX+/ns8p8r72lMBLSNTxEDLyC/yOS0r5JaEIdmWC1qbY1VcoLV5spwrkgOFGc4cd+ItzYJBk7qJItBa",
            "oCTorsz9PVTZpFk06EyvYgOdJDn1mEJuaC22Vii7MbcSEhJuiLW7igToBwkJlzgThoWSoJNzWn6JPSp+Jwd6O9HYBrZStXv0dhye",
            "FJjv3nRu/fr6VOWgj4e/nL8kcfedeJDuDEqtCgd9YtwrtfMGd0e/19cz7lAqI6BcMnG5yd4uVeYIpQkPtww3jak4BM05p27oyFjb",
            "3fR/KRc4M5GWWmwLAn0vMtR0tLF+aT5tdX63gTJ/0iK20kCKN3GhE+nqsdmKQK92J8Ufv6F062kQ0JHEeF0opVsfkQC9iGipKVuJ",
            "0uaRlMtfSFQBOrkLxXxz8jPz0FUYdNZkilfHj0JaImvrb6JLkK3jQvTEpPSnKjht0w867WWqeceNRI0vRUFf9aea9iQPkrPAnOsT",
            "7bzBViI64XdXZzrd03WCPlQXnCvxsKEY6HWVWGGZaaR1xnVHKGhi/ujDsuzQFUlno5Vk9FcM9I4yEv6t74VAb0ESL6RxUNCRxN7D",
            "1llAUDltw/PF/q4H9HFebon1XtLQ6yQAOo2byLOOoHURGkM8Q6r7W3YKCOkFMZhbk1eqPOg0fhi62i3ehJcCehg/YyVb/0LcgVj3",
            "CKZON5JPS+APHiIPWsSRQ2rWbD7oNQKmpxL0lsFAdyFW7svU2YIrJFYVqWyZLOhbbiLmCe06zgV9zEHAsvVOwhwL5pgmvqIeMV88",
            "FaWfI7vLkqAHC5lvR9B054DOElvjVSVkVLyp3xf9G1OlJ2FmpycIzAcdL2iecDraqxzQcYKmCdPHdSDQ+0g3tpyp8rW8k8UuWQr0",
            "UEHzr+GqKzigRwmarob7CpNAoJNIN/YxU+U33B8jHN3ZIgP6tmiQvywezf+bA7qhaMsJUzzIqQO2xCGRmT1p/4jZbdgySAa0+K67",
            "EbiyGxP0XmHTz+FTPH8AaHciaGYQ63n5R5uULCQAuoawecdM1qgWIzOHfiwnMGVI4lI9YviCqRKAK/iIV/gPHPTfgPvBs9QnMkHX",
            "Fje9FlOeCmhYSxLoU8Bsmyw78QoXwkF/o2u2upQFOkGXU2gxQLsjCfRO4L0cBlTYCw66P8B8G0x7Hwv0Sl0x7B8A2gHwReksaDTG",
            "RGrCQUOy7W1ZnTZGenKXK3ia0X6AdhAJ9BKmymLgbNBMUqGgM0HHsGCxlhQW6M4Ay47s9T1HiHvelgEnlD0gJI5Do+A3QOsC3Clh",
            "wwDdQFcXgYzwvUmgI5gqUdj1oIOs9kF79EUQ6O8x/UoM0DUgprGE4qsA5e7EWDNwcQfaiBcNBf0XCPRyTL86AzQoHoVNpO8AlANJ",
            "oKOYKjG63laEcCMH9DEQ6HmM1uEtB5nGthSlApRfJoGOBnbJ5yHNjYKCPgWi8S2m78kA7QAxfcxc+z5A2Z8E+iemCp7cCNmSj3ZA",
            "QZ8HgcZDoFUZoN0gps9j4QiAsi/c/YunUARAmnsQCvoqCDQO04lRBjom/KYePynR18G+tQjs+lBIcy9BQT8Agcb2DGZZM0BDvuRg",
            "g6V3nAVouxK9d8xI1lzs8rmACl3gK0OA4weVwWhcY/X2EQDTdXUl4NkTQTOnEXhEeBugwpfgoCFxfTy1508W6PkA0x2APiEzIR5q",
            "wFyZfqjLBzYcDvojgPl+mHYMC/SPANOv4rlZENDEFD7miQZ99Hjm0Y9w0Lt1TR5nsUCnAVIGFrKTRniymgR6E0vDT08CfAWZCIu7",
            "sPnymawweAw4mZMdYfkAAnoaCTTzdVqVn/ENGqL5oMWP3CN8prsxE/QGYdOEvWvBSNegxl0x4Y7OXcLV/UcGdJyw+a14Xqw1E/QD",
            "4eDQSOZ/yBdvImgfmP8tvaJodful8jpqyUcVDjAXMwCP9J+4bjkIaOvbpDt7m6UyHr9+umBtzbKlQC8VNI+vpbSxHNCi0aEQTcdz",
            "TK2dHUvDZ+5aprdYZYclc+/aCFn3Imh6cm411UnItN0VXHU4DHQY3NtxQXYY7SebTXpI9sV+hdunvhIyPZHzH8r679LtYD5fseVb",
            "mX+k86PbCZi3SuBMVImgM0XCWS7J8pmXhVKOuEmvD/S/SRQIVsyVT0SPFrgTUgbyWC5o7ajA6V/RBL0wIGgUR6qdOdBbxUu9sMI0",
            "edDaJ/zFPelF680HrcVycyjfJWjdd4OCHkqsnRkH7EnSGM2pp4+mB7QWzjE/gt9faLuyZnJMtyBtvpkB5Uxa6fHSnayvwElXT9IH",
            "mkP6FaJOfyHQWcyBEjUlngck8c1L4ieXU5hj7ijw4+11VtMJWpvL2I9U8yJJ47y1EGgtk7XIb01caRyEcyZN8zVse6+p2JL3dh+j",
            "nQ3qNo9xqqbwXvBYD4p5p3DyuXhdkRhoTfuZluvsvop8Ps9QCdDkj5Bl1oD6LHKnhROJr/AQ5qEd4ud1ZHxB2tZZelwy+fJIJAxa",
            "02YTn5d2lIMkrpaVAF2DbGsO06tAOxji8nvYmNNqo8YUyHkd1/HDrJvTDiTO8ISA1k4OxjpJ2x9oF7+NZIR8MGkaayc6IdWxUJKn",
            "FHUxWfXhHrEKO4Hm5KiiW0fqjzxKvXImAoHWtBuzinqvrF+Mo175h9z370aTrTGd4uVZR6eeWD8hqH379l3HLt6VrGlqQee6y9eM",
            "7Z1jfvCE/55hXJVkDwWtadnH1k7pk2O648hFuxjHoWa1lOKMypOPJj3J3Ebf+LqmSIrnlDDC60rVKWFZ0h//oazZ2CcltM62aNAx",
            "qNhAT5LljNzIR/sms4Ou6ywZ9DXnYgN9Wsch9OTd8Bznp0eK5YJOa4mKDXQPec7Uz76xZzFdMy0WdF9UbKAjkB5ZQOkY7IzctufA",
            "zSR80dZJPeiPUHGBzhiv7+sV9vdkBg9U8QCwnX/gTrZMKw7o2EQojfdFo3aXtwMtP+iEdArlaGPeZ4Xsj4Lamdoc3wZ2GXFAL6gF",
            "O6o7c5BwePSsDaynpPjp5UzeY5H7qHOOn699EdDOxGaEz4NEckEj5z2ASu4GiMehz6Ia53UP/TCxpnkMbnZkKzpOET2+/f7UCojw",
            "meYOfNDIeqrwWfxbXREEdE77b4qa3uGDFIgrbbH8iLe7zWmjUDOP5hNwNfPk7UYCoBHyPiL2rhoLyqzIy31z/FnIdHIPpEbGUp9F",
            "D57qKIF53sbCxE2zTftBYqCR1TsCPW9PYwQHjWzDBT6nk9hGEWdUhuqh4SeaN4rmdef2Ty6OYsZ0aaARqhTFWw2GQHOFCrM566zL",
            "4gx7k+2RMvEXdzgSApiHWO42k1CHwxP348MPkTjonBXSQUYlj1bz0nvpoHOWbLtZk7oZTkilhNGDSDW5yqWaf7zpKkk3YUVP83BE",
            "owmbL2TcP7d1ZAUkALro/ocaH8QR371XlwfzvxDLAo1Qw/eiiK7fMytfd0aK5Tf6oyM2UW897UeTgyvT9s9pDmwDq0cXRGzCTGMJ",
            "l9e+20jINBt03lJ35s67JoNRzPBqqBikF+PBFN5ZXzVwWNj0hUvmh4/v6S3xbRh2jy4crpv1Gvl5RMTC8DFDOnsIm+aDzovX950w",
            "fcGSr6dPfKuzCyomKfULnfTNVuipCL9HS4sY6KciNRiLj4wRBmh10oz1GcjDrQ3QysTrAWsGNc7KAK1KwpkT91/rG6AVieNZtjNh",
            "joMBWo247WavRi9M9DJAKxGbWJ6DJbatAVqFlDvM9WWtqm6AViCV+VG6rG0hNgZo3dJdxBOeENbAAK1XOouFAq/vmBxUx8YArUMq",
            "HhTP+TsXNfWtzo2cDNBSYhsJT465fSkBk6PfmnhY3Xu89kS6Oxqgc2S9qsTO7U86e9i/JiXpn5Y1QCP77apI7y5MkPf/17wo3ACN",
            "UKmB5xSR3lrwwsT3J2Z7GqBzpOxPikiPyTNXiVDyggE6b5GoaKA+nb8QIpS0NUDny4C7Skh7yIOer+pW8BOXzlgQaFRxVJIC0EGi",
            "oPE9SJNU3QmeMbsPWZRU26Mf9EBR0IeLI4UzX6Yozt4vBumbqBf0IFHQMzlbL3QInoo1xNJAI/vPM54SaOzrjyeV3UQZ84TZh87I",
            "8sTv9NMBjX3uarK6ezA/H3QGskSx6rTkjjzovsKga5kmfP1aSd0t1L9mYnqNHbJQcZp5XxZ0G2HQyKHoB8SjlcLwPFXE9HfIgsUh",
            "dK8U53QHcdCo+uPMtMzpihNJHJY+zpmPq4AsW9ptlngvbkEA0Ah1XJE7SqVu76y++V7Lcg/DubGhC7J8qTAkFrhnNqtJnqIdIa+e",
            "diKDp2+1Ymq+TS2/6qikSKWAcWvFpyGp3QrUduGJ1+WRIdx3Y/D0bbdEQPcq1HAzd5xkBxoYBX3W9YLHLD6YxDgE7N6aIp+Ebh1f",
            "dDN46l6fEn3z/wcqCBA/R89+6gAAAABJRU5ErkJggg==",
        };
    }
}
