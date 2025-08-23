import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
  // Simulate up to 100 virtual users
  stages: [
    { duration: '30s', target: 20 },
    { duration: '1m', target: 50 },
    { duration: '2m', target: 100 },
    { duration: '30s', target: 0 },
  ],
  thresholds: {
    // The error rate must be below 1%
    http_req_failed: ['rate<0.01'],
    // 95% of requests must complete within 200ms
    http_req_duration: ['p(95)<200'], 
  },
};

// The main function for the virtual user
export default function () {
  // TODO: Replace with a real API endpoint
  const res = http.get('http://localhost:5000/api/flows');

  check(res, { 'status was 200': (r) => r.status == 200 });
  sleep(1);
}
