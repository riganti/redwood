module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'jsdom',
  setupFiles: [ "./Resources/Scripts/tests/setup.js", "./Resources/Scripts/knockout-latest.debug.js" ]
};
