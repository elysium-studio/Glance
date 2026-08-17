namespace Glance.Application.Abstractions;

public sealed class GlanceQuickConverterSetupException(Exception innerException) :
    Exception("The converter could not be set up.", innerException);
