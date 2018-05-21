import {  BotFrameworkAdapter } from "botbuilder";
import * as restify from 'restify';
import { botLogic } from './2-logic';

// Create server
const server = restify.createServer();
server.listen(3978, () => {
    console.log(`${server.name} listening to ${server.url}`);
});

// Create adapter
const adapter = new BotFrameworkAdapter({
    appId: undefined,
    appPassword: undefined
});

// Route /api/messages requests to bot logic
server.post('/api/messages', (req, res, next) => {
    adapter.processActivity(req, res, botLogic);
});