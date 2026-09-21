using System.Text.Json;
using System.Text.Json.Serialization;
using AmharcAgent.Core.Domain;
using AmharcAgent.Core.Interfaces;
using AmharcAgent.Core.Models;

namespace AmharcAgent.Core.Contracts;

/// <summary>
/// Explicit JSON wire converter for the canonical AMHARC Sport vocabulary.
/// </summary>
public sealed class SportWireJsonConverter : JsonConverter<Sport>
{
    public override Sport Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return value switch
        {
            "gaelic-football" => Sport.GaelicFootball,
            "hurling" => Sport.Hurling,
            "camogie" => Sport.Camogie,
            "ladies-football" => Sport.LadiesFootball,
            _ => throw new JsonException(
                $"Unsupported Sport wire value '{value}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        Sport value,
        JsonSerializerOptions options)
    {
        var wireValue = value switch
        {
            Sport.GaelicFootball => "gaelic-football",
            Sport.Hurling => "hurling",
            Sport.Camogie => "camogie",
            Sport.LadiesFootball => "ladies-football",
            _ => throw new JsonException(
                $"Unsupported Sport value '{value}'.")
        };

        writer.WriteStringValue(wireValue);
    }
}

/// <summary>
/// Explicit JSON wire converter for the canonical AMHARC scoring model.
/// </summary>
public sealed class ScoringModelWireJsonConverter :
    JsonConverter<ScoringModel>
{
    public override ScoringModel Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return value switch
        {
            "goals-points" => ScoringModel.GoalsPoints,
            "goals-two-point-one-point" =>
                ScoringModel.GoalsTwoPointOnePoint,
            _ => throw new JsonException(
                $"Unsupported ScoringModel wire value '{value}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ScoringModel value,
        JsonSerializerOptions options)
    {
        var wireValue = value switch
        {
            ScoringModel.GoalsPoints => "goals-points",
            ScoringModel.GoalsTwoPointOnePoint =>
                "goals-two-point-one-point",
            _ => throw new JsonException(
                $"Unsupported ScoringModel value '{value}'.")
        };

        writer.WriteStringValue(wireValue);
    }
}

/// <summary>
/// Explicit JSON wire converter for AMHARC overlay output modes.
/// </summary>
public sealed class OverlayOutputModeWireJsonConverter :
    JsonConverter<OverlayOutputMode>
{
    public override OverlayOutputMode Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();

        return value switch
        {
            "clean" => OverlayOutputMode.Clean,
            "programme" => OverlayOutputMode.Programme,
            "overlay-only" => OverlayOutputMode.OverlayOnly,
            "operator-preview" => OverlayOutputMode.OperatorPreview,
            _ => throw new JsonException(
                $"Unsupported OverlayOutputMode wire value '{value}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        OverlayOutputMode value,
        JsonSerializerOptions options)
    {
        var wireValue = value switch
        {
            OverlayOutputMode.Clean => "clean",
            OverlayOutputMode.Programme => "programme",
            OverlayOutputMode.OverlayOnly => "overlay-only",
            OverlayOutputMode.OperatorPreview => "operator-preview",
            _ => throw new JsonException(
                $"Unsupported OverlayOutputMode value '{value}'.")
        };

        writer.WriteStringValue(wireValue);
    }
}
