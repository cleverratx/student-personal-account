#!/usr/bin/env python3
"""Проверяет имя ветки, заголовок PR и сообщения коммитов по правилам из docs/contributing.md.

В CI значения приходят из переменных окружения BRANCH, TITLE, BASE_SHA и HEAD_SHA.
Локально можно передать их аргументами:

    python3 .github/scripts/check_conventions.py --branch feat/schedule-week-view \
        --title "feat(schedule): расписание на неделю со свайпом по дням"

Имя ветки и заголовок PR проверяются строго, потому что заголовок PR становится
коммитом в main. Сообщения коммитов в ветке только дают предупреждения: при squash
они в main не попадают.
"""

import argparse
import os
import re
import subprocess
import sys

TYPES = ("feat", "fix", "refactor", "test", "docs", "ci", "chore", "spike")
AGENT_PREFIXES = ("claude", "copilot", "cursor", "codex", "gemini")

BRANCH_RE = re.compile(
    r"^(?P<type>" + "|".join(TYPES) + r")/(?:\d+-)?(?P<area>[a-z0-9]+)(?:-[a-z0-9]+)+$"
)
AGENT_BRANCH_RE = re.compile(r"^(?:" + "|".join(AGENT_PREFIXES) + r")/[A-Za-z0-9._/-]+$")
TITLE_RE = re.compile(
    r"^(?P<type>" + "|".join(TYPES) + r")\((?P<area>[a-z0-9]+(?:-[a-z0-9]+)*)\): (?P<text>\S.*)$"
)
CYRILLIC_RE = re.compile(r"[а-яё]", re.IGNORECASE)
UPPER_CYRILLIC_RE = re.compile(r"^[А-ЯЁ]")
VAGUE_TEXTS = {"правки", "правка", "фикс", "фиксы", "обновление", "изменения", "доработки", "wip", "fix", "update"}
SOFT_TITLE_LIMIT = 72


class Report:
    def __init__(self) -> None:
        self.errors = 0

    def error(self, message: str) -> None:
        self.errors += 1
        print(f"::error::{message}")

    def warning(self, message: str) -> None:
        print(f"::warning::{message}")

    def ok(self, message: str) -> None:
        print(f"ok: {message}")


def check_text(text: str, where: str, report: Report, strict: bool) -> None:
    say = report.error if strict else report.warning
    if not CYRILLIC_RE.search(text):
        say(f"{where}: после двоеточия пишем по-русски: \"{text}\"")
    if UPPER_CYRILLIC_RE.match(text):
        say(f"{where}: после двоеточия пишем с маленькой буквы: \"{text}\"")
    if text.endswith("."):
        say(f"{where}: точку в конце не ставим: \"{text}\"")
    if text.strip().lower() in VAGUE_TEXTS:
        say(f"{where}: \"{text}\" ничего не говорит о результате, напишите, что именно изменилось")


def check_branch(branch: str, report: Report):
    """Возвращает (тип, область) для обычной ветки и None для ветки агента."""
    if AGENT_BRANCH_RE.match(branch):
        report.ok(f"ветка агента {branch}, её имя не проверяем, только заголовок PR")
        return None
    match = BRANCH_RE.match(branch)
    if not match:
        report.error(
            f"Ветка \"{branch}\" не по шаблону <тип>/<область>-<суть>. "
            f"Тип: {', '.join(TYPES)}. Только латиница в нижнем регистре, цифры и дефисы, "
            f"например feat/schedule-week-view"
        )
        return None
    report.ok(f"ветка {branch}")
    return match.group("type"), match.group("area")


def check_title(title: str, expected, report: Report) -> None:
    errors_before = report.errors
    match = TITLE_RE.match(title)
    if not match:
        report.error(
            f"Заголовок PR \"{title}\" не по шаблону <тип>(<область>): <что сделано>, "
            f"например feat(schedule): расписание на неделю со свайпом по дням"
        )
        return
    if expected and (match.group("type"), match.group("area")) != expected:
        report.error(
            f"Префикс заголовка PR должен совпадать с веткой: ждём {expected[0]}({expected[1]}):, "
            f"а в заголовке {match.group('type')}({match.group('area')}):"
        )
    check_text(match.group("text"), "Заголовок PR", report, strict=True)
    if len(title) > SOFT_TITLE_LIMIT:
        report.warning(f"Заголовок PR длиннее {SOFT_TITLE_LIMIT} символов, GitHub и git log его обрежут")
    if report.errors == errors_before:
        report.ok(f"заголовок PR \"{title}\"")


def commit_subjects(base: str, head: str):
    result = subprocess.run(
        ["git", "log", "--no-merges", "--format=%s", f"{base}..{head}"],
        capture_output=True, text=True, check=False,
    )
    if result.returncode != 0:
        print(f"::warning::Не получилось прочитать коммиты ветки: {result.stderr.strip()}")
        return []
    return [line for line in result.stdout.splitlines() if line.strip()]


def check_commits(subjects, expected, report: Report) -> None:
    for subject in subjects:
        match = TITLE_RE.match(subject)
        if not match:
            report.warning(f"Коммит \"{subject}\" не по шаблону <тип>(<область>): <что сделано>")
            continue
        if expected and (match.group("type"), match.group("area")) != expected:
            report.warning(f"Коммит \"{subject}\": префикс отличается от ветки, ждём {expected[0]}({expected[1]}):")
        check_text(match.group("text"), f"Коммит \"{subject}\"", report, strict=False)
    if subjects:
        report.ok(f"коммитов проверено: {len(subjects)}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--branch", default=os.environ.get("BRANCH", ""))
    parser.add_argument("--title", default=os.environ.get("TITLE", ""))
    parser.add_argument("--base", default=os.environ.get("BASE_SHA", ""))
    parser.add_argument("--head", default=os.environ.get("HEAD_SHA", ""))
    args = parser.parse_args()

    report = Report()
    expected = None
    if args.branch:
        expected = check_branch(args.branch, report)
    if args.title:
        check_title(args.title, expected, report)
    if args.base and args.head:
        check_commits(commit_subjects(args.base, args.head), expected, report)
    if not (args.branch or args.title):
        print("Нечего проверять: передайте --branch и --title или переменные BRANCH и TITLE")
        return 2

    if report.errors:
        print(f"Ошибок: {report.errors}. Правила лежат в docs/contributing.md")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
