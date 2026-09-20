SHELL := /bin/sh

.PHONY: help setup format lint test build security ci

help:
	@printf '%s\n' 'Targets: setup format lint test build security ci'

setup:
	@echo 'Replace with project bootstrap command.'

format:
	@echo 'Replace with project formatter command.'

lint:
	@echo 'Replace with project lint command.'

test:
	@echo 'Replace with project test command.'

build:
	@echo 'Replace with project build command.'

security:
	@echo 'Use repository security workflows and add stack-specific scanners.'

ci: lint test build security
