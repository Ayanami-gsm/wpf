import axios from 'axios'

// 创建 axios 实例（开发环境走 Vite 代理，无需写完整地址）
const request = axios.create({
  baseURL: '/api',
  timeout: 10000
})

/**
 * 登录接口
 * @param {{username: string, password: string}} data
 */
export function login(data) {
  return request.post('/auth/login', data)
}

export default request
