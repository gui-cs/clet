# Makefile for local clet builds and AOT publishing.
#
# CI is the source of truth for releases (.github/workflows/release.yml).
# This Makefile is for local convenience: builds, tests, and AOT publish
# of the clet binary for the current platform.
#
# RID is auto-detected from `uname` on macOS/Linux; override with `make publish RID=linux-x64`.
# On Windows, run from a POSIX shell (Git Bash, WSL) or set RID explicitly.

UNAME_S := $(shell uname -s 2>/dev/null)
UNAME_M := $(shell uname -m 2>/dev/null)

ifeq ($(UNAME_S),Darwin)
  ifeq ($(UNAME_M),arm64)
    DETECTED_RID := osx-arm64
  else
    DETECTED_RID := osx-x64
  endif
else ifeq ($(UNAME_S),Linux)
  ifeq ($(UNAME_M),aarch64)
    DETECTED_RID := linux-arm64
  else
    DETECTED_RID := linux-x64
  endif
else
  DETECTED_RID := win-x64
endif

RID ?= $(DETECTED_RID)

PROJECT     := src/Clet
PUBLISH_DIR := publish

.PHONY: all restore build build-release test publish publish-all clean help doctor

all: build

help:
	@echo "Targets:"
	@echo "  doctor         Check AOT toolchain prerequisites for this platform"
	@echo "  restore        dotnet restore"
	@echo "  build          Debug build (default)"
	@echo "  build-release  Release build"
	@echo "  test           Run unit, integration, and smoke test projects"
	@echo "  publish        AOT publish for current platform (RID=$(RID))"
	@echo "  publish-all    AOT publish for osx-arm64, linux-x64, win-x64"
	@echo "  clean          Remove publish/ and run dotnet clean"
	@echo ""
	@echo "Override RID: make publish RID=linux-x64"
	@echo "If 'make publish' fails, run 'make doctor' to diagnose the toolchain."

# Verify the prerequisites needed for `make publish` (AOT) on this platform.
# AOT compilation invokes the platform-native linker; missing it produces
# cryptic MSBuild errors. This target checks for the linker up front.
doctor:
	@bash scripts/doctor.sh

restore:
	dotnet restore

build: restore
	dotnet build --no-restore

build-release: restore
	dotnet build --no-restore -c Release

test:
	dotnet run --project tests/Clet.UnitTests --no-build
	dotnet run --project tests/Clet.ConfigTests --no-build
	dotnet run --project tests/Clet.IntegrationTests --no-build
	dotnet run --project tests/Clet.SmokeTests --no-build

publish:
	dotnet publish $(PROJECT) -c Release -r $(RID) --self-contained -p:PublishAot=true -o $(PUBLISH_DIR)/$(RID)

publish-all:
	$(MAKE) publish RID=osx-arm64
	$(MAKE) publish RID=linux-x64
	$(MAKE) publish RID=win-x64

clean:
	rm -rf $(PUBLISH_DIR)
	dotnet clean
