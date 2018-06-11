# Node

## Guides
- [Part 1: Building blocks](1-echo/)
- [Part 2: Forwarding messages to *other* users](2-two-users/)
- [Part 3: Connections](3-two-users-with-connections/)
- [Part 4: Connections for a pool of users](4-user-pool-with-connections/)
- [Part 5: Connect to agent sample](5-simple-agent-sample/)

## Running the samples
In one of the subdirectories, run `npm install` to install dependencies, then `tsc` to transpile TypeScript, and then `npm run`. For example:

```bash
cd ./1-echo/
npm install
tsc
npm start
```

This will start the bot on `localhost` as indicated in the console.