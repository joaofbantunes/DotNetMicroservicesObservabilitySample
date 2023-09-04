import http from "k6/http";
import { check } from "k6";

export const options = {
    insecureSkipTLSVerify: true,
  };

export default function () {
  http.post("http://localhost:6001/do-stuff");
}