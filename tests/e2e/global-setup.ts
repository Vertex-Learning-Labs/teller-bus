import { urls } from './playwright.config';

const healthChecks = [
  `${urls.api}/health`,
  `${urls.mockHost}/health`,
  `${urls.workerReads}/health/ready`,
  `${urls.workerPosts}/health/ready`,
];

export default async function globalSetup() {
  const deadline = Date.now() + 60_000;
  for (const url of healthChecks) {
    while (!(await isUp(url))) {
      if (Date.now() > deadline)
        throw new Error(`${url} is not healthy. Start the stack first: npm run stack:up`);
      await new Promise(resolve => setTimeout(resolve, 1_000));
    }
  }
}

async function isUp(url: string) {
  try {
    return (await fetch(url)).ok;
  } catch {
    return false;
  }
}
