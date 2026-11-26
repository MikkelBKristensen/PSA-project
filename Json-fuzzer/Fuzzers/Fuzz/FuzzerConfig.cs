namespace Fuzzers.Fuzz;

public record FuzzerConfig()
{
    public required int Mutations = 10;
    public required bool GenerateFatMutations = false;
}
