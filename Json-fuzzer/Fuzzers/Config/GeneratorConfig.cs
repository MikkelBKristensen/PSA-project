namespace Fuzzers.Config;

public record GeneratorConfig
{
    // Define weights for productions and terminals.
    // Weights are relative within a single rule; all default to 1 (uniform choice).

    // General
    public int MaxDepth = 10;

    // <object>
    public float ObjectMembersWeight = 1f;
    public float ObjectEmptyWeight = 1f;

    // <members>
    public float MembersSingleWeight = 1f;
    public float MembersMultipleWeight = 1f;

    // <value>
    public float ValueStringWeight = 1f;
    public float ValueIntWeight = 1f;
    public float ValueBooleanWeight = 1f;
    public float ValueArrayWeight = 1f;
    public float ValueObjectWeight = 1f;
    public float ValueNullWeight = 1f;

    // <array>
    public float ArrayEmptyWeight = 1f;
    public float ArrayElementsWeight = 1f;

    // <elements>
    public float ElementsSingleWeight = 1f;
    public float ElementsMultipleWeight = 1f;

    // <characters>
    public float CharactersSingleWeight = 1f;
    public float CharactersMultipleWeight = 1f;

    // <char>
    public float CharWeight = 1f;

    // <int>
    public float IntDigitWeight = 1f;
    public float IntNegativeDigitWeight = 1f;
    public float IntNegativeDigitsWeight = 1f;
    public float IntDigitsWeight = 1f;

    // <digits>
    public float DigitsSingleWeight = 1f;
    public float DigitsMultipleWeight = 1f;

    // <digit>
    public float DigitZeroWeight = 1f;
    public float DigitOneNineWeight = 1f;

    // <onenine>
    public float OneNineWeight = 1f;

    // <boolean>
    public float BooleanTrueWeight = 1f;
    public float BooleanFalseWeight = 1f;

    // <ws>
    public float WsEmptyWeight = 1f;
    public float WsSingleWeight = 1f;
    public float WsMultipleWeight = 1f;

    // <wschar>
    public float WsCharEmptyWeight = 1f;
    public float WsCharSpaceWeight = 1f;
    public float WsCharNewlineWeight = 1f;
    public float WsCharTabWeight = 1f;
}
