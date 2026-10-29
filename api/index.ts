import { handle } from '@hono/node-server/vercel'
import { app } from '../server/app.js'

export default handle(app)
