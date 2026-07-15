// Copyright (C) 2026 SharpEmu Emulator Project
// SPDX-License-Identifier: GPL-2.0-or-later

using SharpEmu.HLE;
using SharpEmu.Libs.Agc;
using Xunit;

namespace SharpEmu.ShaderCompiler.Metal.Tests;

/// <summary>
/// Tier 1: golden-source tests. Each fixture's emitted MSL must match the checked-in
/// golden byte-for-byte (modulo line endings), so any codegen change shows up as a
/// reviewable text diff. Set SHARPEMU_UPDATE_MSL_GOLDENS to the repo Goldens directory
/// to regenerate after an intentional change.
/// </summary>
public sealed class MslGoldenTests
{
    public static TheoryData<string> FixtureNames =>
        new(Gen5ComputeFixtures.All.Select(fixture => fixture.Name));

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void EmittedMslMatchesGolden(string fixtureName)
    {
        var fixture = Gen5ComputeFixtures.All.Single(candidate => candidate.Name == fixtureName);
        var shader = Gen5ComputeFixtures.CompileOrThrow(fixture);
        var actual = Normalize(shader.Source);

        var updateDirectory = Environment.GetEnvironmentVariable("SHARPEMU_UPDATE_MSL_GOLDENS");
        if (!string.IsNullOrEmpty(updateDirectory))
        {
            File.WriteAllText(Path.Combine(updateDirectory, $"{fixture.Name}.msl"), actual);
            return;
        }

        var goldenPath = Path.Combine(AppContext.BaseDirectory, "Goldens", $"{fixture.Name}.msl");
        Assert.True(
            File.Exists(goldenPath),
            $"missing golden '{goldenPath}' — set SHARPEMU_UPDATE_MSL_GOLDENS to generate it");
        var expected = Normalize(File.ReadAllText(goldenPath));

        Assert.True(
            expected == actual,
            $"emitted MSL diverges from the golden for '{fixture.Name}'.\n--- actual ---\n{actual}");
    }

    [Fact]
    public void UnsupportedOpcodeFailsLoudly()
    {
        // v_and_b32 decodes fine but sits outside the spike subset; the emitter must
        // name the gap instead of guessing.
        var memory = new FakeGuestMemory();
        memory.AddRegion(Gen5ComputeFixtures.ProgramAddress, [
            0x36040300u, // v_and_b32 v2, v0, v1
            0xBF810000u, // s_endpgm
        ]);
        var ctx = new CpuContext(memory, Generation.Gen5);
        Assert.True(Gen5ShaderTranslator.TryDecodeProgram(
            ctx, Gen5ComputeFixtures.ProgramAddress, out var program, out var decodeError), decodeError);

        var state = new Gen5ShaderState(program!, new uint[16], Metadata: null);
        var evaluation = new Gen5ShaderEvaluation(
            new uint[256],
            new uint[256],
            new Dictionary<uint, IReadOnlyList<uint>>(),
            Array.Empty<Gen5ImageBinding>(),
            Array.Empty<Gen5GlobalMemoryBinding>());

        var compiled = Gen5MslTranslator.TryCompileComputeShader(
            state, evaluation, 1, 1, 1, out _, out var error);

        Assert.False(compiled);
        Assert.Contains("VAndB32", error, StringComparison.Ordinal);
    }

    private static string Normalize(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
