# HttpExtensions — validation problems

Every validation failure answers with one RFC 7807 document carrying a machine-readable code the
client can switch on and localize.

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
    "reason": [{ "type": "required", "detail": "The Reason field is required." }],
    "name": [{
      "type": "too_long",
      "detail": "The field Name must be a string with a maximum length of 200.",
      "parameters": { "max": 200 }
    }]
  }
}
```

`type` names what went wrong and never the field it is keyed under (`required`, not
`name_required`), so a client handles it generically wherever it appears. `parameters` carries what
a client needs to write its own sentence in its own language.

## Vocabulary

Draw codes from [`ValidationCodes`](ValidationCodes.cs) and parameter keys from
[`ValidationParameters`](ValidationParameters.cs). Two validators catching the same condition must
answer with the same code — that is the point of a shared list, and it applies to parameter keys
just as much: a client looking up `max` finds nothing under `maximum`.

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

## Attribute failures

DataAnnotations records a message and discards the constraint that produced it: the `200` in
`[StringLength(200)]` is gone by the time the failure is reported. The sentence it leaves behind
does still contain that `200`, so [`ValidationMessageCodes`](ValidationMessageCodes.cs) recognises
the wording and lifts the value back out, and
[`ValidationProblemFactory`](ValidationProblemFactory.cs) assembles the document.

Nothing here validates anything and nothing here reflects. The framework decides whether a value is
valid; this reads what it said afterwards. Being pure string work it needs no generator, no
registration and no `[RequiresUnreferencedCode]` — the assembly builds with `IsAotCompatible` and is
clean, so it can be published from a trimmed or ahead-of-time minimal API as-is.

### What this costs

The wording is the contract, and that has three consequences worth knowing before you rely on it.

**A reworded message loses its code.** The failure is still reported, with its message intact, as
`invalid`. `ValidationMessageCodesTests` asserts every pattern against the message its attribute
actually produces, so a framework reword fails the build on the next SDK bump rather than in
production.

**An explicit `ErrorMessage` never matches.** `[Required(ErrorMessage = "'key' must be provided")]`
is recognised by nothing and reports as `invalid`. Roughly 5% of the repo's validation attributes
set one today.

**Renamed and non-numeric values are approximate.** Model state keys the CLR name, so a property
renamed by `[JsonPropertyName]` is reported under its camel-cased CLR name rather than the name the
client sent — the same thing the previous envelope did. And a bound lifted out of prose is the
framework's rendering of it, so `[Range(typeof(DateTime), "2020-01-01", …)]` yields
`"2020-01-01 00:00:00"`, not the declared literal.

All three are fixable by reading the model's attributes instead of its messages — with reflection,
which is not trim-safe, or with a source generator, which is more machinery. This takes the cheap
option deliberately.

## Turning it on

The coded document is behind `FeatureFlagKeys.CodedValidationProblems`. With the flag off, every
surface answers exactly as it did before.

It replaces a body clients are already parsing, so each surface opts in separately —
`ModelStateValidationFilterAttribute.TryCodedProblem` offers it, and only the internal Api takes it
today. The public API keeps its published shape, which is versioned on its own terms.

## Known gaps

- `ExceptionHandlerFilterAttribute` still answers with `ErrorResponseModel`, so
  `throw new BadRequestException(modelState)` bypasses all of this.
- `.Produces<BitwardenValidationProblemDetails>(400)` is not declared on endpoints, so OpenAPI does
  not yet describe the coded shape.
