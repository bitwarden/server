# HttpExtensions — validation problems

Every validation failure, wherever it is detected, answers with one RFC 7807 document carrying a
machine-readable code the client can switch on and localize.

Before, a request that failed its DataAnnotations answered with the `ErrorResponseModel` envelope
while a request its handler rejected answered with a problem document. Same endpoint, same field,
two bodies and two key conventions — a client needed both parsers, and only one of them carried a
code.

```json
{
  "type": "validation_error",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "reason": [{ "type": "required", "detail": "Reason is required." }],
    "name": [{
      "type": "too_long",
      "detail": "Name must be 200 characters or shorter.",
      "parameters": { "max": 200 }
    }],
    "members[1].email": [{ "type": "invalid_email", "detail": "Email is not an address." }]
  }
}
```

`errors` is keyed by the property **as the client sent it** — camel-cased, `[JsonPropertyName]`
honoured, nested and indexed. `type` names what went wrong and never the field it is keyed under
(`required`, not `name_required`), so a client handles it generically wherever it appears.
`parameters` carries what a client needs to write its own sentence in its own language.

## Vocabulary

Draw codes from [`ValidationCodes`](ValidationCodes.cs) and parameter keys from
[`ValidationParameters`](ValidationParameters.cs). Two validators catching the same condition must
answer with the same code — that is the whole point of a shared list, and it applies to parameter
keys just as much: a client looking up `max` finds nothing under `maximum`.

`parameters` carries the limit that was breached and never anything derived from the value that
breached it — a length ceiling, never the string that overran it. Error bodies travel further than
the request did.

## Returning one from a handler

```csharp
return TypedResults.BitwardenValidationProblem(
[
    ("reason", new ErrorCode(ValidationCodes.Required, "Reason is required.")),
]);
```

Domain errors implementing `IValidationError` render through the same document — see
`ValidationErrorTypedResultsExtensions` in Core, which is the one layer that can see both
`IValidationError` and this project.

## Attribute failures

DataAnnotations records a message and throws away the constraint that produced it: the `200` in
`[StringLength(200)]` is gone by the time the failure is reported.
[`ValidationCodeMap`](ValidationCodeMap.cs) recovers it, and
[`ValidationProblemFactory`](ValidationProblemFactory.cs) turns the result into the document above.

Nothing here validates anything. The framework decides whether a value is valid; this only names
what it found. A path the map does not recognise is still reported, as
`ValidationCodes.Invalid` with its original message — a 400 with an empty `errors` map would tell a
client less than nothing.

### Two ways a code is resolved

| | Detection | Lookup | Trimming / AOT |
| --- | --- | --- | --- |
| **MVC controllers** | model binding fills `ModelState` | reflection over the request model | not supported — see below |
| **Minimal APIs** | `AddValidation()` | map generated at build time | supported |

`TryResolve` prefers a generated map and falls back to reflection. `TryResolveRegistered` reads
only the generated map and never reflects; that is the entry point for anything that must survive
publishing.

### Opting a model into the generated map

Mark the root. Everything reachable from it is covered, so nested models need nothing.

```csharp
[GenerateValidationCodes]
public sealed class CreateThingRequestModel
{
    [Required]
    public string? Reason { get; init; }

    [StringLength(200)]
    public string? Name { get; init; }
}
```

The framework's `[ValidatableType]` works as a trigger too, since a minimal API being validated
already carries it.

## How the generator identifies a failure

For a property with **one** constraint, the path alone is enough — an error on `name` can only have
come from the one attribute there. No message is involved, so framework wording is irrelevant. This
covers the large majority of properties.

For a property with **several**, the generator reconstructs each attribute and asks it how it words
itself:

```csharp
new("required", static name => new RequiredAttribute().FormatErrorMessage(name)),
new("too_long", null, [new("max", 50)]),
```

Asking beats storing a copy: a framework release that rewords a message moves both sides together.

`MaxLengthAttribute`, `MinLengthAttribute` and `CompareAttribute` have
`[RequiresUnreferencedCode]` constructors, so generated code cannot build them to ask. Only *n − 1*
candidates need identifying though, so one is left as the fallback — above, `required` identifies
itself and anything else on that property must be the length failure. `BWVAL001` warns when two
constraints on one property are both unaskable, because then the choice would be a guess.

## Why not the built-in validation generator

ASP.NET Core ships its own validation source generator behind `AddValidation()`, and this project
uses it: it does the detection, the graph walking, the collections, the cycle detection. What it
does not do is name the failure. Its 400 looks like this:

```json
{
  "title": "One or more validation errors occurred.",
  "errors": {
    "Reason": ["The Reason field is required."],
    "Seats":  ["The field Seats must be between 1 and 100."]
  }
}
```

A `Dictionary<string, string[]>` of English prose, keyed by the CLR name. There is no code to
switch on and no `200` to render a localized message from — the constraint that produced each
sentence was read and then discarded. That is the gap, and it is not a gap a configuration setting
closes:

- **A generator cannot consume another generator's output.** Roslyn does not feed generated source
  back in as input, so ours cannot build on theirs. Vendoring their generator wholesale is the only
  way to reuse its walking logic, which means owning it.
- **There is nothing to hook.** Their validation error type carries `Name`, `Path`, `ErrorMessage`
  and `Container` — no code, no parameters. The `OnValidationError` callback that could have
  enriched errors as they were recorded was `[Experimental]` in .NET 10 and is removed in .NET 11.
- **Their emitted types are not a public contract.** `ValidatableTypeInfo` and friends are being
  removed from the public API and emitted as `file` classes, so reading their metadata at runtime
  is not a supported seam either.

So this generator is complementary rather than a replacement. It emits **no validation logic at
all** — just a lookup table saying what each path's constraints were called and what values they
carried, which the framework had at compile time and threw away at runtime.

### Why a generator rather than reading the attributes at runtime

Because of trimming. Recovering a constraint by reflection means `Type.GetProperty` and
`GetCustomAttributes` over an open-ended object graph, which the trim analyzer reports as
`IL2070`/`IL2075` — the property or the attribute may not survive publish. There is no annotation
that fixes it, because the graph is not knowable from the signature. Baking the table at build time
is the only way the lookup still works after an AOT publish.

On the controller surface none of that applies, which is why MVC reflects instead.

## Why MVC does not use the generator

`AddControllers()` is annotated:

> `[RequiresUnreferencedCode("MVC does not currently support trimming or native AOT.")]`

A generated map for the controller surface would make an un-publishable path look publishable.
The reflective resolver is used there instead and declares itself with
`[RequiresUnreferencedCode]`, so a caller that does care is told at compile time rather than
discovering it after publish.

This project builds with `IsAotCompatible`, so anything new that reflects without saying so fails
the build.

## Turning it on

The coded document is behind `FeatureFlagKeys.CodedValidationProblems`. With the flag off, every
surface answers exactly as it did before.

It replaces a body clients are already parsing, so each surface opts in separately —
`ModelStateValidationFilterAttribute.TryCodedProblem` offers it, and only the internal Api takes it
today. The public API keeps its published shape, which is versioned on its own terms.

## Known gaps

- `ExceptionHandlerFilterAttribute` still answers with `ErrorResponseModel`, so
  `throw new BadRequestException(modelState)` bypasses all of this.
- Minimal APIs are not migrated. The generated path is tested but no endpoint uses it yet; PAM's
  filter still calls `Validator.TryValidateObject`, which is what blocks AOT there.
- `.Produces<BitwardenValidationProblemDetails>(400)` is not declared on endpoints, so OpenAPI does
  not yet describe the coded shape.
