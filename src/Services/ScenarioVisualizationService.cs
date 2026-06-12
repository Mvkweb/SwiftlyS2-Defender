using Microsoft.Extensions.Logging;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2_Defender.Interfaces;
using SwiftlyS2_Defender.Models;
using System.Collections.Generic;

namespace SwiftlyS2_Defender.Services;

public sealed class ScenarioVisualizationService : IScenarioVisualizationService
{
    private readonly ISwiftlyCore _core;
    private readonly ILogger _logger;
    private readonly List<uint> _beamIndices = new();
    private readonly List<uint> _textIndices = new();

    public ScenarioVisualizationService(ISwiftlyCore core, ILogger logger)
    {
        _core = core;
        _logger = logger;
    }

    public void DrawScenario(Scenario scenario)
    {
        ClearVisualizations();

        // Draw Anchor
        if (scenario.Anchor != null)
        {
            var anchorPos = new Vector(scenario.Anchor.X, scenario.Anchor.Y, scenario.Anchor.Z);
            CreateBeam(anchorPos, new Color(0, 255, 0, 255)); // Green for anchor
            CreateText(new Vector(anchorPos.X, anchorPos.Y, anchorPos.Z + 70.0f), "Anchor");
        }

        // Draw Bots
        for (int i = 0; i < scenario.Bots.Count; i++)
        {
            var bot = scenario.Bots[i];
            if (bot.Frames.Count > 0)
            {
                var startFrame = bot.Frames[0];
                var startPos = new Vector(startFrame.X, startFrame.Y, startFrame.Z);
                CreateBeam(startPos, new Color(255, 0, 0, 255)); // Red for bots
                CreateText(new Vector(startPos.X, startPos.Y, startPos.Z + 70.0f), $"Bot #{i + 1}");
            }
        }

        // Draw Grenades
        for (int i = 0; i < scenario.Grenades.Count; i++)
        {
            var grenade = scenario.Grenades[i];
            var startPos = new Vector(grenade.OriginX, grenade.OriginY, grenade.OriginZ);
            CreateBeam(startPos, new Color(0, 150, 255, 255)); // Blue for grenades
            
            // Format name (e.g. "flashbang_projectile" -> "flashbang")
            string niceName = grenade.GrenadeType.Replace("_projectile", "");
            CreateText(new Vector(startPos.X, startPos.Y, startPos.Z + 70.0f), $"{niceName} #{i + 1}");
        }
    }

    public void ClearVisualizations()
    {
        foreach (var idx in _beamIndices)
        {
            var beam = _core.EntitySystem.GetEntityByIndex<CBeam>(idx);
            if (beam != null && beam.IsValid) beam.Despawn();
        }
        _beamIndices.Clear();

        foreach (var idx in _textIndices)
        {
            var text = _core.EntitySystem.GetEntityByIndex<CPointWorldText>(idx);
            if (text != null && text.IsValid) text.Despawn();
        }
        _textIndices.Clear();
    }

    private void CreateBeam(Vector start, Color color)
    {
        var beam = _core.EntitySystem.CreateEntityByDesignerName<CBeam>("beam");
        if (beam == null) return;

        beam.StartFrame = 0;
        beam.FrameRate = 0;
        beam.LifeState = 1;
        beam.Width = 5.0f;
        beam.EndWidth = 5.0f;
        beam.Amplitude = 0;
        beam.Speed = 50;
        beam.BeamFlags = 0;
        beam.BeamType = BeamType_t.BEAM_HOSE;
        beam.FadeLength = 10.0f;
        beam.Render = color;
        beam.TurnedOff = false;

        beam.EndPos.X = start.X;
        beam.EndPos.Y = start.Y;
        beam.EndPos.Z = start.Z + 100.0f;

        beam.Teleport(start, new QAngle(0, 0, 0), Vector.Zero);
        beam.DispatchSpawn();

        beam.LifeStateUpdated();
        beam.StartFrameUpdated();
        beam.FrameRateUpdated();
        beam.WidthUpdated();
        beam.EndWidthUpdated();
        beam.AmplitudeUpdated();
        beam.SpeedUpdated();
        beam.BeamFlagsUpdated();
        beam.BeamTypeUpdated();
        beam.FadeLengthUpdated();
        beam.TurnedOffUpdated();
        beam.EndPosUpdated();
        beam.RenderUpdated();

        _beamIndices.Add(beam.Index);
    }

    private void CreateText(Vector pos, string textStr)
    {
        var text = _core.EntitySystem.CreateEntityByDesignerName<CPointWorldText>("point_worldtext");
        if (text == null) return;

        text.MessageText = textStr;
        text.FontSize = 48.0f;
        text.Color = new Color(255, 255, 255, 255);
        text.Fullbright = true;
        text.WorldUnitsPerPx = 0.1f;
        text.JustifyHorizontal = PointWorldTextJustifyHorizontal_t.POINT_WORLD_TEXT_JUSTIFY_HORIZONTAL_CENTER;
        text.JustifyVertical = PointWorldTextJustifyVertical_t.POINT_WORLD_TEXT_JUSTIFY_VERTICAL_CENTER;
        text.ReorientMode = PointWorldTextReorientMode_t.POINT_WORLD_TEXT_REORIENT_AROUND_UP;
        text.Enabled = true;

        text.Teleport(pos, new QAngle(0, 0, 90), Vector.Zero);
        text.DispatchSpawn();

        text.MessageTextUpdated();
        text.FontSizeUpdated();
        text.ColorUpdated();
        text.FullbrightUpdated();
        text.WorldUnitsPerPxUpdated();
        text.JustifyHorizontalUpdated();
        text.JustifyVerticalUpdated();
        text.ReorientModeUpdated();
        text.EnabledUpdated();

        _textIndices.Add(text.Index);
    }
}
