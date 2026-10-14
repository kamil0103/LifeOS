import { notify } from './notify'
import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  headers: {
    'Content-Type': 'application/json',
  },
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  // Don't override Content-Type for FormData - let browser set boundary
  if (config.data instanceof FormData) {
    delete config.headers['Content-Type']
  }
  return config
})

// Share one refresh across simultaneous requests after the access token expires.
let refreshInFlight: Promise<string> | null = null

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const originalRequest = error.config

    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true
      const currentAccessToken = localStorage.getItem('accessToken')
      if (currentAccessToken && originalRequest.headers.Authorization !== `Bearer ${currentAccessToken}`) {
        originalRequest.headers.Authorization = `Bearer ${currentAccessToken}`
        return api(originalRequest)
      }
      const refreshToken = localStorage.getItem('refreshToken')

      if (refreshToken) {
        try {
          if (!refreshInFlight) {
            refreshInFlight = axios.post('/api/auth/refresh', { refreshToken })
              .then(({ data }) => {
                localStorage.setItem('accessToken', data.accessToken)
                localStorage.setItem('refreshToken', data.refreshToken)
                return data.accessToken as string
              })
              .finally(() => { refreshInFlight = null })
          }

          const accessToken = await refreshInFlight
          originalRequest.headers.Authorization = `Bearer ${accessToken}`
          return api(originalRequest)
        } catch {
          localStorage.removeItem('accessToken')
          localStorage.removeItem('refreshToken')
          window.location.href = '/login'
        }
      }
    }

    if (error.response?.status >= 500 || (!error.response && error.code !== 'ERR_CANCELED')) {
      notify(error.response?.data?.detail || 'Could not reach LifeOS. Check your connection and try again.', 'error')
    }
    return Promise.reject(error)
  }
)

export default api
