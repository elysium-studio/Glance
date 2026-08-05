using Microsoft.UI.Xaml.Media;
using System;
using Windows.Foundation;
using Windows.UI;

namespace Glance.Weather.WinUI;

internal static class WeatherScenePalette
{
    public static Brush CreateBackground(WeatherViewModel viewModel)
    {
        (Color start, Color end) = GetBackgroundColors(viewModel);

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop { Color = start, Offset = 0 },
                new GradientStop { Color = end, Offset = 1 }
            }
        };
    }

    private static (Color Start, Color End) GetBackgroundColors(WeatherViewModel viewModel)
    {
        (Color start, Color end) = GetTimelineColors(GetTimelineHour(viewModel.WeatherHour,
            viewModel.SunriseHour,
            viewModel.SunsetHour));

        if (viewModel.WeatherTemperature == WeatherTemperature.Hot && viewModel.WeatherTime is not WeatherTimeOfDay.Night)
        {
            start = Blend(start, Color.FromArgb(255, 184, 65, 26), 0.44f);
            end = Blend(end, Color.FromArgb(255, 250, 153, 61), 0.44f);
        }

        if (viewModel.WeatherSky == WeatherSky.Cloudy)
        {
            start = Blend(start, Color.FromArgb(255, 42, 57, 72), 0.58f);
            end = Blend(end, Color.FromArgb(255, 92, 110, 126), 0.58f);
        }
        else if (viewModel.WeatherSky == WeatherSky.PartlyCloudy)
        {
            start = Blend(start, Color.FromArgb(255, 55, 80, 104), 0.25f);
            end = Blend(end, Color.FromArgb(255, 124, 145, 161), 0.25f);
        }

        if (viewModel.WeatherEffect == WeatherEffect.Thunderstorm)
        {
            start = Blend(start, Color.FromArgb(255, 21, 18, 45), 0.72f);
            end = Blend(end, Color.FromArgb(255, 51, 55, 88), 0.72f);
        }
        else if (viewModel.WeatherEffect == WeatherEffect.Rain)
        {
            start = Blend(start, Color.FromArgb(255, 16, 42, 67), 0.45f);
            end = Blend(end, Color.FromArgb(255, 45, 83, 112), 0.45f);
        }
        else if (viewModel.WeatherEffect == WeatherEffect.Snow)
        {
            start = Blend(start, Color.FromArgb(255, 48, 85, 115), 0.28f);
            end = Blend(end, Color.FromArgb(255, 153, 185, 207), 0.28f);
        }
        else if (viewModel.WeatherEffect == WeatherEffect.Fog)
        {
            start = Blend(start, Color.FromArgb(255, 55, 67, 78), 0.55f);
            end = Blend(end, Color.FromArgb(255, 126, 139, 148), 0.55f);
        }

        return (start, end);
    }

    private static double GetTimelineHour(double hour, double sunrise, double sunset)
    {
        double solarNoon = sunrise + ((sunset - sunrise) / 2);

        if (hour >= sunrise && hour < solarNoon)
        {
            return 6 + ((hour - sunrise) / Math.Max(1, solarNoon - sunrise) * 6.5);
        }

        if (hour >= solarNoon && hour < sunset)
        {
            return 12.5 + ((hour - solarNoon) / Math.Max(1, sunset - solarNoon) * 6.5);
        }

        double nightDuration = 24 - sunset + sunrise;
        double nightElapsed = hour >= sunset ? hour - sunset : 24 - sunset + hour;
        return (19 + (nightElapsed / Math.Max(1, nightDuration) * 11)) % 24;
    }

    private static (Color Start, Color End) GetTimelineColors(double hour)
    {
        (Color Start, Color End) night = (Color.FromArgb(255, 7, 18, 50), Color.FromArgb(255, 32, 50, 101));
        (Color Start, Color End) dawn = (Color.FromArgb(255, 65, 72, 139), Color.FromArgb(255, 242, 151, 124));
        (Color Start, Color End) morning = (Color.FromArgb(255, 48, 139, 214), Color.FromArgb(255, 150, 211, 239));
        (Color Start, Color End) afternoon = (Color.FromArgb(255, 20, 105, 202), Color.FromArgb(255, 83, 182, 235));
        (Color Start, Color End) evening = (Color.FromArgb(255, 31, 112, 190), Color.FromArgb(255, 245, 181, 105));
        (Color Start, Color End) dusk = (Color.FromArgb(255, 50, 54, 112), Color.FromArgb(255, 204, 95, 133));
        double normalizedHour = ((hour % 24) + 24) % 24;

        return normalizedHour switch
        {
            < 5.25 => InterpolateTimeline(night, dawn, normalizedHour / 5.25),
            < 8 => InterpolateTimeline(dawn, morning, (normalizedHour - 5.25) / 2.75),
            < 12.5 => InterpolateTimeline(morning, afternoon, (normalizedHour - 8) / 4.5),
            < 17 => InterpolateTimeline(afternoon, evening, (normalizedHour - 12.5) / 4.5),
            < 19.5 => InterpolateTimeline(evening, dusk, (normalizedHour - 17) / 2.5),
            < 21.5 => InterpolateTimeline(dusk, night, (normalizedHour - 19.5) / 2),
            _ => night
        };
    }

    private static (Color Start, Color End) InterpolateTimeline((Color Start, Color End) from,
        (Color Start, Color End) to,
        double amount)
    {
        float easedAmount = (float)(amount * amount * (3 - (2 * amount)));
        return (Blend(from.Start, to.Start, easedAmount), Blend(from.End, to.End, easedAmount));
    }

    private static Color Blend(Color source, Color target, float amount) => Color.FromArgb(255,
        (byte)(source.R + ((target.R - source.R) * amount)),
        (byte)(source.G + ((target.G - source.G) * amount)),
        (byte)(source.B + ((target.B - source.B) * amount)));
}
