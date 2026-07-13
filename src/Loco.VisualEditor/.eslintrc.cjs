/**
 * ESLint config for the Visual Editor.
 *
 * There was no ESLint config file at all, so `npm run lint` (which the CI was
 * also never running) failed to start under ESLint 8. This is the standard
 * Vite + React + TypeScript setup; the strict-but-noisy rules that would fail
 * the large amount of pre-existing `any`/console usage are downgraded to
 * warnings so lint can run green today and the debt can be paid down
 * incrementally rather than blocking every commit.
 */
module.exports = {
  root: true,
  env: { browser: true, es2021: true, node: true },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
  ],
  parser: '@typescript-eslint/parser',
  parserOptions: { ecmaVersion: 'latest', sourceType: 'module' },
  plugins: ['@typescript-eslint', 'react-hooks', 'react-refresh'],
  ignorePatterns: ['dist', 'node_modules', '*.config.ts', '*.config.js', '.eslintrc.cjs'],
  rules: {
    // Correctness rule kept as a hard error - it catches the "hooks after an
    // early return" crashes that were fixed across ValidationPanel/PropertyPanel/
    // TemplateGallery. Regressions here should fail the build.
    'react-hooks/rules-of-hooks': 'error',
    'react-hooks/exhaustive-deps': 'warn',
    'react-refresh/only-export-components': 'off',
    // Pre-existing debt (76 `any`, 61 console.*): keep visible as warnings,
    // don't fail the build. Tighten to 'error' once the debt is worked down.
    '@typescript-eslint/no-explicit-any': 'warn',
    '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
    'no-console': 'off',
    'no-empty': ['warn', { allowEmptyCatch: true }],
    // Stylistic rules downgraded to warnings: these fire only in pre-existing
    // code (regex escapes, switch-case declarations, a control-char regex in the
    // sanitizer, a deliberate while(true) poll loop) that this pass does not
    // otherwise touch. Kept visible, not build-blocking.
    'no-useless-escape': 'warn',
    'no-case-declarations': 'warn',
    'no-control-regex': 'warn',
    'no-constant-condition': ['warn', { checkLoops: false }],
  },
};
