# .ai/ - IDE-Agnostic AI Assistant Configuration

This folder contains all AI assistant configurations and skills for XerahS, designed to work across multiple IDEs and AI coding assistants.

## 📁 Structure

```
.ai/
├── instructions.md       # Main agent instructions (single source of truth)
├── skills/              # Reusable skills/prompts for specific tasks
│   ├── avalonia-api/
│   ├── build-windows-exe/
│   ├── design-ui-window/
│   └── ...
├── workflows/           # Complex multi-step workflows
└── README.md           # This file
```

## 🔧 IDE Integration

This centralized structure is referenced by:

- **GitHub Copilot** (VSCode, Visual Studio): `.github/copilot-instructions.md`
- **Cursor/Windsurf**: Native support via `.ai/` folder
- **Continue.dev**: `.continue/config.json`
- **Antigravity**: `.antigravity/rules.md`
- **Cline/Aider**: Direct reference to `.ai/instructions.md`

## 📚 Skills

Skills are specialized instruction sets for domain-specific tasks. Each skill folder contains:
- `SKILL.md` - Detailed instructions for that capability
- Supporting files, examples, or templates

To use a skill, agents read the SKILL.md file and follow its instructions.

## 🎯 Benefits

1. **Single Source of Truth**: Update instructions once, applies to all IDEs
2. **Version Control**: All AI configs tracked in git
3. **Portability**: Works across VSCode, Cursor, Windsurf, Visual Studio, etc.
4. **Maintainability**: Clear organization, easy to update
5. **Team Collaboration**: Consistent AI behavior for all developers

## 🚀 Usage

### For Developers
Just use your preferred IDE/AI assistant. The configs automatically reference this folder.

### For AI Assistants
Read `.ai/instructions.md` as the primary instruction set, and load specific skills from `.ai/skills/` as needed.

---

**Last Updated**: February 20, 2026
