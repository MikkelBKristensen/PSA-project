# PSA-project

## Findings

- When generating JSON, we sometime had key collisions on shorter JSON, where a key had already been defined. Should we try to mitigate this?

- Before properly implementing depth, we would have instances of tests never terminating, since it spent so much time generating what would probably end up being nearly infinite objects.

