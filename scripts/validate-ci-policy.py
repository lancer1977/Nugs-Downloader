#!/usr/bin/env python3
"""Fail closed unless ci.yml keeps package publication on the trusted job."""

from __future__ import annotations

import re
import sys
from pathlib import Path

MAPPING = re.compile(r"^(?P<indent> *)(?P<key>[^:#][^:]*?):(?P<value>.*)$")
ALLOWED_PERMISSION_PARENTS = {(), ("jobs", "test"), ("jobs", "container"), ("jobs", "publish")}
TRUSTED_PUSH = "github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/master')"


def fail(message: str) -> None:
    raise ValueError(f"CI policy validation failed: {message}")


def without_comment(value: str) -> str:
    quote: str | None = None
    for index, char in enumerate(value):
        if char in "\"'":
            quote = None if quote == char else char if quote is None else quote
        elif char == "#" and quote is None and (index == 0 or value[index - 1].isspace()):
            return value[:index].strip()
    return value.strip()


def scalar(value: str) -> str:
    value = without_comment(value)
    if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
        value = value[1:-1]
    return value


def key_name(key: str) -> str:
    key = key.strip()
    if len(key) >= 2 and key[0] == key[-1] and key[0] in "\"'":
        key = key[1:-1]
    return key


def parse(workflow: Path) -> dict[tuple[str, ...], list[str]]:
    values: dict[tuple[str, ...], list[str]] = {}
    stack: list[tuple[int, str]] = []

    for line_number, raw_line in enumerate(workflow.read_text(encoding="utf-8").splitlines(), start=1):
        if "\t" in raw_line[: len(raw_line) - len(raw_line.lstrip())]:
            fail(f"tabs are not supported in YAML indentation (line {line_number})")

        content = raw_line.strip()
        if not content or content.startswith("#") or content.startswith("-"):
            continue

        match = MAPPING.match(raw_line)
        if match is None:
            continue

        indent = len(match.group("indent"))
        while stack and indent <= stack[-1][0]:
            stack.pop()

        key = key_name(match.group("key"))
        value = without_comment(match.group("value"))
        path = tuple(item[1] for item in stack) + (key,)

        if key == "permissions":
            parent = path[:-1]
            if parent not in ALLOWED_PERMISSION_PARENTS:
                fail(f"permissions is not allowed at {'.'.join(path)} (line {line_number})")
            if value:
                fail(f"permissions must use a block mapping at {'.'.join(path)} (line {line_number})")

        if stack and stack[-1][1] == "permissions":
            if key == "<<":
                fail(f"YAML merge keys are not allowed in permissions (line {line_number})")
            if key == "packages":
                permission = scalar(value)
                if permission not in {"read", "write", "none"}:
                    fail(f"packages must be an explicit read, write, or none value (line {line_number})")

        values.setdefault(path, []).append(scalar(value))

        if not value:
            stack.append((indent, key))

    return values


def require_exact(values: dict[tuple[str, ...], list[str]], path: tuple[str, ...], expected: str) -> None:
    actual = values.get(path)
    if actual != [expected]:
        fail(f"{'.'.join(path)} must be exactly {expected!r}; found {actual!r}")


def require_permission_map(
    values: dict[tuple[str, ...], list[str]],
    path: tuple[str, ...],
    expected: dict[str, str],
) -> None:
    actual: dict[str, str | list[str]] = {}
    for candidate, candidate_values in values.items():
        if candidate[:-1] == path:
            actual[candidate[-1]] = candidate_values[0] if len(candidate_values) == 1 else candidate_values
    if actual != expected:
        fail(f"{'.'.join(path)} must be exactly {expected!r}; found {actual!r}")


def validate(workflow: Path) -> None:
    if not workflow.is_file():
        fail(f"workflow not found: {workflow}")

    values = parse(workflow)

    for job in ("test", "container", "publish"):
        if ("jobs", job) not in values:
            fail(f"jobs.{job} is missing")

    require_permission_map(values, ("permissions",), {"contents": "read"})
    require_permission_map(values, ("jobs", "test", "permissions"), {"contents": "read"})
    require_permission_map(values, ("jobs", "container", "permissions"), {"contents": "read"})
    require_permission_map(
        values,
        ("jobs", "publish", "permissions"),
        {"contents": "read", "packages": "write"},
    )
    require_exact(values, ("jobs", "publish", "if"), TRUSTED_PUSH)

    package_writes = [
        path
        for path, permissions in values.items()
        if path[-2:] == ("permissions", "packages") and "write" in permissions
    ]
    if package_writes != [("jobs", "publish", "permissions", "packages")]:
        fail(f"packages: write is only allowed for jobs.publish.permissions; found {package_writes!r}")

    run_values = values.get(("jobs", "test", "steps", "run"), [])
    if "scripts/test-ci-policy.sh" not in run_values:
        fail("test job must execute scripts/test-ci-policy.sh")


def main() -> int:
    workflow = Path(sys.argv[1] if len(sys.argv) > 1 else ".github/workflows/ci.yml")
    try:
        validate(workflow)
    except ValueError as error:
        print(error, file=sys.stderr)
        return 1

    print("CI permission policy passed: validation jobs are read-only and only trusted publish can write packages.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
