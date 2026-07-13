import json
import os

class ExecutionState:
    def __init__(self, file_path):
        self.file_path = file_path
        self.state = self._load()

    def _load(self):
        if os.path.exists(self.file_path):
            with open(self.file_path, "r") as f:
                return json.load(f)
        return {}

    def get(self, key, default=None):
        return self.state.get(key, default)

    def set(self, key, value):
        self.state[key] = value
        self._save()

    def reset(self):
        self.state = {}
        self._save()

    def _save(self):
        with open(self.file_path, "w") as f:
            json.dump(self.state, f, indent=2)