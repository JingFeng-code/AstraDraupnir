import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vitest/config'
import { defineVitestProject } from '@nuxt/test-utils/config'

const appDir = fileURLToPath(new URL('./app', import.meta.url))

export default defineConfig({
  test: {
    projects: [
      await defineVitestProject({
        resolve: { alias: { '~': appDir } },
        // node_modules may be a junction to a sibling worktree; allow files outside the project root
        server: { fs: { strict: false } },
        test: {
          name: 'nuxt',
          include: ['tests/**/*.test.ts'],
          environment: 'nuxt',
        },
      }),
    ],
  },
})
