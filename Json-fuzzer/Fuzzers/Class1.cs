namespace Fuzzers;

public class Class1
{
    public record class Cat(string Name);
    public record class Dog(string Name);
    public record class Bird(string Name);

    public union Pet(Cat, Dog, Bird);
}