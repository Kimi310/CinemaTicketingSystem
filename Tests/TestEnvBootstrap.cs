using System.Runtime.CompilerServices;

namespace BookingService.Tests;

/// <summary>
/// Disables the Testcontainers "Ryuk" resource-reaper sidecar BEFORE the
/// Testcontainers library is touched. Ryuk needs to pull testcontainers/ryuk
/// from Docker Hub, which on a local dev box without registry credentials
/// often hangs / cancels — and then every test fails with OperationCanceled
/// inside PostgresFixture.InitializeAsync. We don't need it: PostgresFixture
/// explicitly disposes the container in DisposeAsync.
/// </summary>
internal static class TestEnvBootstrap
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }
}

