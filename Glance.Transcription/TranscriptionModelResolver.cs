namespace Glance.Transcription;

public static class TranscriptionModelResolver
{
    public static string? ResolveInstalledModel(ITranscriptionModelCatalog catalog,
        ITranscriptionModelSelection selection)
    {
        if (!string.IsNullOrWhiteSpace(selection.SelectedModelId) &&
            catalog.Models.Any(model => string.Equals(model.Id, selection.SelectedModelId, StringComparison.OrdinalIgnoreCase)) &&
            catalog.IsInstalled(selection.SelectedModelId))
        {
            return selection.SelectedModelId;
        }

        if (!string.IsNullOrWhiteSpace(catalog.DefaultModelId) && catalog.IsInstalled(catalog.DefaultModelId))
        {
            return catalog.DefaultModelId;
        }

        return catalog.Models.FirstOrDefault(model => catalog.IsInstalled(model.Id))?.Id;
    }
}
